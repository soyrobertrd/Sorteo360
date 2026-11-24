using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// Lo que el worker hace por cada campaña que sale del canal (spec <c>04</c> §1.1): leer la cola
/// pendiente, entregarla por lotes y dejar la campaña en <c>sent</c> cuando no queda ninguna fila
/// pendiente.
/// </summary>
public sealed class CampaignHandoffServiceTests
{
    private readonly ICampaignRepository _repository = Substitute.For<ICampaignRepository>();
    private readonly ITemplateRepository _templates = Substitute.For<ITemplateRepository>();
    private readonly ICampaignSendGateway _gateway = Substitute.For<ICampaignSendGateway>();
    private readonly CampaignHandoffService _service;

    private readonly Guid _campaignId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();

    /// <summary>El único rechazo por persona que el transporte llega a ver: una dirección rota.</summary>
    private const string RejectionReason = "RECIPIENT_ADDRESS_INVALID";

    private const string PortalBaseUrl = "https://app.pgti360.test";

    /// <summary>Lo que dijo el evento cuando alguien apretó lanzar, ya copiado en la fila.</summary>
    private static readonly CampaignEventValues EventValues =
        new("Roadshow Madrid 2026", "Madrid, España", "12 de junio de 2026");

    /// <summary>
    /// Una plantilla de campaña con los seis marcadores dentro. El cuerpo tiene que llegar al
    /// transporte tal cual: acá no se sustituye nada.
    /// </summary>
    private static readonly Template Letter = new(
        TemplateKey.RegistrationInvite,
        AllowedLanguage.Espanol,
        "Te invitamos a {{evento}}",
        "<html>Estimado {{nombre}}, {{lugar}} el {{fecha}}. "
        + "<a href=\"{{enlace_registro}}\">Registrarse</a> <a href=\"{{enlace_baja}}\">Baja</a></html>",
        TemplatePurpose.Campaign,
        isCampaignSelectable: true);

    public CampaignHandoffServiceTests()
    {
        _service = new CampaignHandoffService(
            _repository,
            _templates,
            _gateway,
            new PortalSettings { BaseUrl = PortalBaseUrl });

        _templates.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<Template>, BaseError>(Maybe.From(Letter)));

        _repository.RecordHandoffAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<RecipientHandoffOutcome>>(),
                Arg.Any<IReadOnlyList<RecipientTokenHashes>>(),
                Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());
        _repository.CompleteHandoffAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());
        _repository.ReadStateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<string>, BaseError>(Maybe.From(CampaignState.Sending)));
    }

    [Fact]
    public async Task HandOffAsync_ReturnsEveryRecipientHandedOffAndTheCampaignCompleted_WhenTheTransportAccepts()
    {
        var recipients = Pending(2);
        GivenCampaign(CampaignState.Sending, recipients);
        GivenGatewayAccepts();

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _repository.Received(1).RecordHandoffAsync(
            _campaignId,
            Arg.Is<IReadOnlyList<RecipientHandoffOutcome>>(outcomes =>
                outcomes.Count == 2 && outcomes.All(o => o.SendStatus == RecipientSendStatus.HandedOff)),
            Arg.Any<IReadOnlyList<RecipientTokenHashes>>(),
            Arg.Any<CancellationToken>());

        await _repository.Received(1).CompleteHandoffAsync(_campaignId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cada lote firma con la dirección que la fila copió al lanzar (PGTI-168), y sin ella el
    /// transporte firma con la general.
    /// </summary>
    [Theory]
    [InlineData("campanas.madrid@sorteo360.local")]
    [InlineData(null)]
    public async Task HandOffAsync_ReturnsBatchesFromTheCopiedSenderEmail_WhenTheCampaignIsHandedOff(string? senderEmail)
    {
        _repository.ReadHandoffAsync(_campaignId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CampaignHandoff>, BaseError>(
                Maybe.From(new CampaignHandoff(
                    CampaignState.Sending,
                    _eventId,
                    TemplateKey.RegistrationInvite,
                    AllowedLanguage.Espanol,
                    EventValues,
                    Pending(2),
                    SenderEmail: senderEmail))));
        GivenGatewayAccepts();

        await _service.HandOffAsync(_campaignId, CancellationToken.None);

        _gateway.ReceivedCalls()
            .Select(call => ((SendBatchRequest)call.GetArguments()[0]!).FromAddress)
            .ShouldBe([senderEmail]);
    }

    /// <summary>
    /// Mil y uno son dos llamadas, y la segunda lleva el resto (spec <c>04</c> §2.2). Trocear es
    /// trabajo de quien llama: por encima del tope el transporte contesta un 400, así que una
    /// campaña de tres mil sin trocear no se entrega a nadie.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReturnsTwoBatches_WhenThereAreAThousandAndOneRecipients()
    {
        GivenCampaign(CampaignState.Sending, Pending(1001));
        GivenGatewayAccepts();

        await _service.HandOffAsync(_campaignId, CancellationToken.None);

        var batches = _gateway.ReceivedCalls()
            .Select(call => (SendBatchRequest)call.GetArguments()[0]!)
            .Select(request => request.Recipients.Count)
            .ToList();

        batches.ShouldBe([1000, 1]);
    }

    /// <summary>
    /// Un rechazo por persona viaja dentro de un lote aceptado: esa fila queda <c>failed</c> con
    /// su motivo, las demás <c>handed_off</c>, y la campaña llega igual a <c>sent</c>. No es una
    /// avería del lote (spec <c>04</c> §2.1).
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReturnsTheRejectedOneFailedAndTheRestHandedOff_WhenOneRecipientIsRejected()
    {
        var recipients = Pending(3);
        var rejected = recipients[1];

        GivenCampaign(CampaignState.Sending, recipients);
        GivenGatewayRejects(rejected.RecipientId);

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _repository.Received(1).RecordHandoffAsync(
            _campaignId,
            Arg.Is<IReadOnlyList<RecipientHandoffOutcome>>(outcomes =>
                outcomes.Count(o => o.SendStatus == RecipientSendStatus.HandedOff) == 2
                && outcomes.Single(o => o.SendStatus == RecipientSendStatus.Failed).RecipientId == rejected.RecipientId
                && outcomes.Single(o => o.SendStatus == RecipientSendStatus.Failed).FailureReason == RejectionReason),
            Arg.Any<IReadOnlyList<RecipientTokenHashes>>(),
            Arg.Any<CancellationToken>());

        await _repository.Received(1).CompleteHandoffAsync(_campaignId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// El lote entero se cayó: no hay nada que anotar, las filas se quedan <c>pending</c> y la
    /// campaña se queda en <c>sending</c>. Es el estado desde el que una persona reanuda, y es
    /// también lo que impide escribirle dos veces a quien ya recibió (spec <c>04</c> §5).
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReturnsFailureRecordingNothingAndStillSending_WhenTheTransportRefusesTheBatch()
    {
        GivenCampaign(CampaignState.Sending, Pending(2));

        _gateway.SendAsync(Arg.Any<SendBatchRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<SendBatchResult, BaseError>(
                new BaseError("el transporte no contestó", "NOTIFICATIONS_UNAVAILABLE", ErrorKind.Unavailable)));

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();

        await _repository.DidNotReceive().RecordHandoffAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<RecipientHandoffOutcome>>(),
            Arg.Any<IReadOnlyList<RecipientTokenHashes>>(),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CompleteHandoffAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// El segundo lote se cae y el primero ya estaba anotado. Es lo que hace que reanudar siga por
    /// la fila 1001 en vez de volver a escribirle a las primeras mil (spec <c>04</c> §5), y lo que
    /// justifica anotar lote a lote en vez de al final.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReturnsFailureWithTheFirstBatchRecorded_WhenTheSecondBatchFails()
    {
        GivenCampaign(CampaignState.Sending, Pending(1001));

        var calls = 0;

        _gateway.SendAsync(Arg.Any<SendBatchRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                calls++;

                return calls == 1
                    ? Result.Success<SendBatchResult, BaseError>(new SendBatchResult([
                        .. call.Arg<SendBatchRequest>().Recipients
                            .Select(recipient => RecipientHandoffOutcome.Accepted(recipient.RecipientId)),
                    ]))
                    : Result.Failure<SendBatchResult, BaseError>(
                        new NotificationsUnavailableError("no respondió"));
            });

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();

        await _repository.Received(1).RecordHandoffAsync(
            _campaignId,
            Arg.Is<IReadOnlyList<RecipientHandoffOutcome>>(outcomes => outcomes.Count == 1000),
            Arg.Any<IReadOnlyList<RecipientTokenHashes>>(),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CompleteHandoffAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Alguien cancela entre el primer lote y el segundo. El resto no sale: mirar el estado una
    /// sola vez al empezar dejaría salir dos mil correos de una campaña que ya nadie quiere
    /// mandar, que es justo lo que permitir cancelar en pleno envío existe para evitar.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReturnsWithoutSendingTheRest_WhenCancelledBetweenTwoBatches()
    {
        GivenCampaign(CampaignState.Sending, Pending(1001));
        GivenGatewayAccepts();

        _repository.ReadStateAsync(_campaignId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<string>, BaseError>(Maybe.From(CampaignState.Cancelled)));

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _gateway.ReceivedCalls().Count().ShouldBe(1);
        await _repository.DidNotReceive().CompleteHandoffAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Una campaña cuyo resto eran supresiones no tiene cola que entregar, y aun así tiene que
    /// llegar a <c>sent</c>: lo que la cierra es que no le quede ninguna fila pendiente, no que
    /// el transporte haya dicho algo.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReturnsTheCampaignCompletedWithoutTheTransport_WhenNothingIsPending()
    {
        GivenCampaign(CampaignState.Sending, []);

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _gateway.ReceivedCalls().ShouldBeEmpty();
        await _repository.Received(1).CompleteHandoffAsync(_campaignId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cancelar en pleno envío está permitido (spec <c>03</c> §2), así que el id que salió del
    /// canal puede apuntar a una campaña que ya nadie quiere mandar. Se mira el estado antes de
    /// entregar, no después.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReturnsWithoutReachingTheTransport_WhenTheCampaignIsNoLongerSending()
    {
        GivenCampaign(CampaignState.Cancelled, Pending(2));
        GivenGatewayAccepts();

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _gateway.ReceivedCalls().ShouldBeEmpty();
        await _repository.DidNotReceive().CompleteHandoffAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// El nombre del evento viaja una vez para todo el lote y no una por persona. Separar en vez
    /// de mezclar es lo que mantiene mil copias del mismo texto fuera del cable, y es también por
    /// qué una clave no puede estar en las dos bolsas (spec <c>04</c> §2).
    /// </summary>
    [Fact]
    public async Task HandOffAsync_PutsTheEventValuesInSharedDataOnly_WhenItBuildsABatch()
    {
        GivenCampaign(CampaignState.Sending, Pending(3));
        GivenGatewayAccepts();

        await _service.HandOffAsync(_campaignId, CancellationToken.None);

        var batch = SingleBatch();

        batch.SharedData[SupportedPlaceholder.EventName].ShouldBe(EventValues.Name);
        batch.SharedData[SupportedPlaceholder.EventLocation].ShouldBe(EventValues.Location);
        batch.SharedData[SupportedPlaceholder.EventDates].ShouldBe(EventValues.Dates);

        batch.Recipients.ShouldAllBe(recipient =>
            !recipient.Data.Keys.Any(key => batch.SharedData.ContainsKey(key)));
    }

    /// <summary>
    /// Convocatorias arma y no sustituye: el asunto y el cuerpo llegan al transporte con sus
    /// marcadores intactos, porque quien sustituye es el lado que recibe.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_SendsTheLetterUnsubstituted_WhenItBuildsABatch()
    {
        GivenCampaign(CampaignState.Sending, Pending(1));
        GivenGatewayAccepts();

        await _service.HandOffAsync(_campaignId, CancellationToken.None);

        var batch = SingleBatch();

        batch.Subject.ShouldBe(Letter.Subject);
        batch.BodyHtml.ShouldBe(Letter.BodyHtml);
        batch.BodyHtml.ShouldContain("{{nombre}}");
        batch.BodyHtml.ShouldContain("{{enlace_baja}}");
        batch.EventId.ShouldBe(_eventId);
        batch.Language.ShouldBe(AllowedLanguage.Espanol);
    }

    /// <summary>
    /// Una fila importada que solo trae dirección recibe el saludo neutro, no un hueco. Saltársela
    /// dejaría fuera en silencio a gente que la coordinadora cree invitada (spec <c>02</c> §2.2).
    /// </summary>
    [Fact]
    public async Task HandOffAsync_SendsTheNeutralGreeting_WhenTheRowCarriesOnlyAnAddress()
    {
        var nameless = new PendingHandoffRecipient(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "   ",
            "   ",
            "sin.nombre@acme.com",
            HasTokens: false);

        GivenCampaign(CampaignState.Sending, [nameless]);
        GivenGatewayAccepts();

        await _service.HandOffAsync(_campaignId, CancellationToken.None);

        SingleBatch().Recipients.Single().Data[SupportedPlaceholder.RecipientName]
            .ShouldBe(SupportedPlaceholder.NeutralRecipientName);
    }

    /// <summary>
    /// Cada persona lleva sus dos enlaces, con un token distinto cada uno, y lo que se guarda es
    /// solo el SHA-256 de lo que se le mandó: un volcado de la base no entrega ningún enlace que
    /// funcione (spec <c>06</c> §2.1).
    /// </summary>
    [Fact]
    public async Task HandOffAsync_RecordsOnlyTheHashes_WhenItMintsTheTwoTokens()
    {
        GivenCampaign(CampaignState.Sending, Pending(2));
        GivenGatewayAccepts();

        await _service.HandOffAsync(_campaignId, CancellationToken.None);

        var batch = SingleBatch();
        var hashes = RecordedHashes().ToDictionary(entry => entry.RecipientId);

        hashes.Count.ShouldBe(2);

        var tokens = new List<string>();

        foreach (var recipient in batch.Recipients)
        {
            var registration = TokenIn(recipient.Data[SupportedPlaceholder.RegistrationLink], "t");
            var unsubscribe = TokenIn(recipient.Data[SupportedPlaceholder.UnsubscribeLink], "u");

            // 32 bytes en base64url sin relleno, y el de baja separado del de registro: un token
            // de registro está pensado para reenviarse, así que compartirlo haría que el clic del
            // reenvío diera de baja a la persona original (spec 06 §2.4).
            registration.Length.ShouldBe(43);
            unsubscribe.ShouldNotBe(registration);

            hashes[recipient.RecipientId].RegistrationTokenHash.ShouldBe(Sha256(registration));
            hashes[recipient.RecipientId].UnsubscribeTokenHash.ShouldBe(Sha256(unsubscribe));

            recipient.Data[SupportedPlaceholder.RegistrationLink]
                .ShouldStartWith($"{PortalBaseUrl}/publico/registro#t=");

            tokens.AddRange([registration, unsubscribe]);
        }

        // Y no se reparte el mismo token entre dos personas.
        tokens.Distinct(StringComparer.Ordinal).Count().ShouldBe(tokens.Count);
    }

    /// <summary>
    /// Una fila pendiente que ya guarda sus hashes es una escritura que aterrizó a medias: su
    /// correo ya salió con un enlace que funciona. No vuelve al transporte —sería un segundo
    /// correo— y no se le acuña nada, porque un token nuevo pisaría el hash y mataría ese enlace.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_MintsNothingAndSkipsTheTransport_WhenTheRecipientAlreadyHoldsAHash()
    {
        GivenCampaign(CampaignState.Sending, Pending(2, hasTokens: true));
        GivenGatewayAccepts();

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _gateway.ReceivedCalls().ShouldBeEmpty();

        await _repository.Received(1).RecordHandoffAsync(
            _campaignId,
            Arg.Is<IReadOnlyList<RecipientHandoffOutcome>>(outcomes =>
                outcomes.Count == 2 && outcomes.All(o => o.SendStatus == RecipientSendStatus.HandedOff)),
            Arg.Is<IReadOnlyList<RecipientTokenHashes>>(hashes => hashes.Count == 0),
            Arg.Any<CancellationToken>());

        await _repository.Received(1).CompleteHandoffAsync(_campaignId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Falta la plantilla del idioma del evento. No se cae al español (spec <c>02</c> §3): la
    /// entrega se detiene con sus filas <c>pending</c>, que es lo que una persona puede arreglar.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_StopsWithItsRowsPending_WhenTheTemplateIsMissing()
    {
        GivenCampaign(CampaignState.Sending, Pending(2));
        GivenGatewayAccepts();

        _templates.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<Template>, BaseError>(Maybe<Template>.None));

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CampaignNotRenderableError.ErrorCode);
        _gateway.ReceivedCalls().ShouldBeEmpty();
        await _repository.DidNotReceive().CompleteHandoffAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Una campaña lanzada antes de que existieran las columnas del evento no tiene con qué armar
    /// el correo. Se detiene en vez de mandar un hueco donde va el nombre del evento, que es la
    /// misma decisión que toma el paso 4 del lanzamiento, un paso más tarde.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_StopsBeforeTheTransport_WhenTheCampaignHasNoEventValues()
    {
        _repository.ReadHandoffAsync(_campaignId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CampaignHandoff>, BaseError>(
                Maybe.From(new CampaignHandoff(
                    CampaignState.Sending,
                    _eventId,
                    TemplateKey.RegistrationInvite,
                    AllowedLanguage.Espanol,
                    EventValues: null,
                    Pending(2)))));

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CampaignNotRenderableError.ErrorCode);
        _gateway.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>
    /// Y si no queda a quién escribirle, la plantilla no se lee siquiera: lo que cierra una
    /// campaña es que no le quede ninguna fila pendiente, no que haya copia en su idioma.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReadsNoTemplate_WhenNothingIsPending()
    {
        GivenCampaign(CampaignState.Sending, []);

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _templates.DidNotReceive().GetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <b>La prueba de la reanudación.</b> Se cae el segundo lote, y la vuelta siguiente sale con
    /// la cola que quedó y con nada más: las mil primeras no se vuelven a mandar. Es la única
    /// razón por la que este servicio no necesita idempotencia del otro lado en el caso normal
    /// (spec <c>04</c> §5), y lo que lo sostiene es que la lectura del worker trae solo las filas
    /// que siguen en <c>pending</c>.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_SendsOnlyTheRemainder_WhenItRunsAgainAfterASecondBatchFailed()
    {
        var recipients = Pending(1001);

        GivenCampaign(CampaignState.Sending, recipients);

        var calls = 0;

        _gateway.SendAsync(Arg.Any<SendBatchRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                calls++;

                return calls == 2
                    ? Result.Failure<SendBatchResult, BaseError>(
                        new NotificationsUnavailableError("no respondió"))
                    : Result.Success<SendBatchResult, BaseError>(new SendBatchResult([
                        .. call.Arg<SendBatchRequest>().Recipients
                            .Select(recipient => RecipientHandoffOutcome.Accepted(recipient.RecipientId)),
                    ]));
            });

        (await _service.HandOffAsync(_campaignId, CancellationToken.None)).IsFailure.ShouldBeTrue();

        var mailed = Batches().SelectMany(batch => batch.Recipients.Select(r => r.RecipientId)).ToList();

        // El primer lote quedó anotado entero como entregado, el segundo no anotó nada, y la
        // campaña no se cerró: sus mil una filas se reparten en mil `handed_off` y una `pending`.
        await _repository.Received(1).RecordHandoffAsync(
            _campaignId,
            Arg.Is<IReadOnlyList<RecipientHandoffOutcome>>(outcomes =>
                outcomes.Count == 1000
                && outcomes.All(outcome => outcome.SendStatus == RecipientSendStatus.HandedOff)),
            Arg.Any<IReadOnlyList<RecipientTokenHashes>>(),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CompleteHandoffAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        var tail = recipients.Skip(1000).Select(recipient => recipient.RecipientId).ToList();

        // La reanudación relee, y la lectura del worker trae solo lo que sigue pendiente: acá es
        // la fila 1001 y nada más.
        GivenCampaign(CampaignState.Sending, [.. recipients.Skip(1000)]);

        (await _service.HandOffAsync(_campaignId, CancellationToken.None)).IsSuccess.ShouldBeTrue();

        var resumed = Batches().Skip(2).ToList();

        resumed.Count.ShouldBe(1);
        resumed[0].Recipients.Select(recipient => recipient.RecipientId).ShouldBe(tail);

        // Y a las primeras mil no se les volvió a escribir: las que salieron en la reanudación no
        // están entre las que ya se habían anotado.
        resumed[0].Recipients.ShouldAllBe(recipient => !mailed.Take(1000).Contains(recipient.RecipientId));
    }

    /// <summary>
    /// <c>retry-failed</c> devuelve filas a <c>pending</c> bajo una campaña que ya está
    /// <c>sent</c>, porque lo que la cerró fue quedarse sin pendientes (spec <c>04</c> §5). El
    /// worker tiene que entregarlas y volver a cerrarla: negarse dejaría la cola reabierta sin
    /// nadie que la mande.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_DeliversAndClosesAgain_WhenTheCampaignIsAlreadySent()
    {
        GivenCampaign(CampaignState.Sent, Pending(2));
        GivenGatewayAccepts();

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        SingleBatch().Recipients.Count.ShouldBe(2);
        await _repository.Received(1).CompleteHandoffAsync(_campaignId, Arg.Any<CancellationToken>());
    }

    /// <summary>Los lotes que llegaron al transporte, en orden.</summary>
    private IReadOnlyList<SendBatchRequest> Batches() =>
    [
        .. _gateway.ReceivedCalls().Select(call => (SendBatchRequest)call.GetArguments()[0]!),
    ];

    #region El mensaje directo

    /// <summary>
    /// Un mensaje directo arma su bolsa compartida con el asunto y el texto de su fila, en el lugar
    /// de los tres valores del evento, y a cada persona le acuña solo el token de baja: no hay
    /// evento al que inscribirse (PGTI-174).
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReturnsABatchWithTheMessageAndNoRegistrationLink_WhenTheCampaignIsADirectMessage()
    {
        GivenDirectMessage("Novedades", "Primera línea\nSegunda línea", Pending(1));
        GivenGatewayAccepts();

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var batch = SingleBatch();
        batch.EventId.ShouldBeNull();
        batch.SharedData.ShouldBe(new Dictionary<string, string>
        {
            ["asunto"] = "Novedades",
            ["mensaje"] = "Primera línea\nSegunda línea",
        });

        var data = batch.Recipients.Single().Data;
        data.Keys.ShouldBe(["nombre", "enlace_baja"], ignoreOrder: true);

        var hashes = RecordedHashes().Single();
        hashes.RegistrationTokenHash.ShouldBeNull();
        hashes.UnsubscribeTokenHash.ShouldBe(Sha256(TokenIn(data["enlace_baja"], "u")));
    }

    /// <summary>
    /// Un mensaje directo cuya fila no guarda el asunto o el texto no tiene con qué armar el
    /// correo: se detiene antes del transporte en vez de mandar un hueco.
    /// </summary>
    [Fact]
    public async Task HandOffAsync_ReturnsCampaignNotRenderable_WhenADirectMessageHasNoMessage()
    {
        GivenDirectMessage("Novedades", message: null, Pending(1));

        var result = await _service.HandOffAsync(_campaignId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CampaignNotRenderableError.ErrorCode);
        await _gateway.DidNotReceive().SendAsync(Arg.Any<SendBatchRequest>(), Arg.Any<CancellationToken>());
    }

    private void GivenDirectMessage(string? subject, string? message, IReadOnlyList<PendingHandoffRecipient> recipients) =>
        _repository.ReadHandoffAsync(_campaignId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CampaignHandoff>, BaseError>(
                Maybe.From(new CampaignHandoff(
                    CampaignState.Sending,
                    EventId: null,
                    TemplateKey.DirectMessage,
                    AllowedLanguage.Espanol,
                    EventValues: null,
                    recipients,
                    subject,
                    message))));

    #endregion

    /// <summary>Los hashes que se le pasaron al repositorio, de todos los lotes.</summary>
    private IEnumerable<RecipientTokenHashes> RecordedHashes() =>
        _repository.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ICampaignRepository.RecordHandoffAsync))
            .SelectMany(call => (IReadOnlyList<RecipientTokenHashes>)call.GetArguments()[2]!);

    /// <summary>El token que lleva un enlace en su fragmento.</summary>
    private static string TokenIn(string link, string key)
    {
        var marker = $"#{key}=";

        link.ShouldContain(marker);

        return link[(link.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
    }

    private static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private void GivenCampaign(
        string state,
        IReadOnlyList<PendingHandoffRecipient> recipients,
        CampaignEventValues? eventValues = null) =>
        _repository.ReadHandoffAsync(_campaignId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CampaignHandoff>, BaseError>(
                Maybe.From(new CampaignHandoff(
                    state,
                    _eventId,
                    TemplateKey.RegistrationInvite,
                    AllowedLanguage.Espanol,
                    eventValues ?? EventValues,
                    recipients))));

    private void GivenGatewayAccepts() =>
        _gateway.SendAsync(Arg.Any<SendBatchRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Success<SendBatchResult, BaseError>(
                new SendBatchResult([
                    .. call.Arg<SendBatchRequest>().Recipients
                        .Select(recipient => RecipientHandoffOutcome.Accepted(recipient.RecipientId)),
                ])));

    private void GivenGatewayRejects(Guid rejectedRecipientId) =>
        _gateway.SendAsync(Arg.Any<SendBatchRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Success<SendBatchResult, BaseError>(
                new SendBatchResult([
                    .. call.Arg<SendBatchRequest>().Recipients
                        .Select(recipient => recipient.RecipientId == rejectedRecipientId
                            ? RecipientHandoffOutcome.Rejected(recipient.RecipientId, RejectionReason)
                            : RecipientHandoffOutcome.Accepted(recipient.RecipientId)),
                ])));

    private static IReadOnlyList<PendingHandoffRecipient> Pending(int count, bool hasTokens = false) =>
        [
            .. Enumerable.Range(0, count).Select(index => new PendingHandoffRecipient(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Ana",
                "Pérez",
                $"ana{index}@acme.com",
                hasTokens)),
        ];

    /// <summary>El único lote que llegó al transporte.</summary>
    private SendBatchRequest SingleBatch() =>
        (SendBatchRequest)_gateway.ReceivedCalls().Single().GetArguments()[0]!;
}
