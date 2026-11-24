using System.Net;
using System.Security.Cryptography;
using System.Text;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// La entrega, que ocurre después del <c>202</c> y sin navegador detrás (spec <c>04</c> §1.1,
/// ADR-0018).
/// <para>
/// Lo que se cobra acá no es el mapeo sino <b>de quién es el token</b>. Un
/// <c>CancellationToken</c> declarado en un manejador de Minimal API se enlaza a
/// <c>HttpContext.RequestAborted</c>, así que pasarlo al worker haría que los lotes murieran al
/// terminar la petición: correcto en un test que espera el envío, roto en cuanto alguien cierra
/// la pestaña. Por eso el transporte se cuelga a propósito hasta que el <c>202</c> ya está leído.
/// </para>
/// </summary>
public sealed class CampaignHandoffTests : IClassFixture<CampaignHandoffFactory>
{
    /// <summary>Dos contactos contactables, para poder suprimir uno y seguir teniendo campaña.</summary>
    private static readonly StubContact Ana =
        new(Guid.NewGuid(), "Ana", "Pérez", "ana.handoff@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private static readonly StubContact Luis =
        new(Guid.NewGuid(), "Luis", "Gómez", "luis.handoff@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private readonly CampaignHandoffFactory _factory;

    public CampaignHandoffTests(CampaignHandoffFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
        _factory.Gateway.Reset();
    }

    [Fact]
    public async Task Launch_ReturnsASentCampaignWithEveryRecipientHandedOff_WhenTheWorkerFinishes()
    {
        var client = _factory.CreateClient();
        var campaign = await LaunchAsync(client);

        var sent = await WaitUntilSentAsync(client, campaign.Id);

        sent.SentAt.ShouldNotBeNull();
        sent.LaunchedAt.ShouldNotBeNull();
        sent.SentAt!.Value.ShouldBeGreaterThanOrEqualTo(sent.LaunchedAt!.Value);

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.HandedOff);
    }

    /// <summary>
    /// <b>La prueba del ticket.</b> El transporte se queda colgado hasta después de que el
    /// <c>202</c> se haya leído entero, que es el momento en que <c>RequestAborted</c> se cancela.
    /// Si un token de manejador llegara al worker, los lotes morirían ahí y los destinatarios se
    /// quedarían <c>pending</c> para siempre.
    /// </summary>
    [Fact]
    public async Task Launch_ReturnsEveryRecipientHandedOff_WhenTheTransportIsHeldPastTheResponse()
    {
        var client = _factory.CreateClient();

        _factory.Gateway.Hold();

        var campaign = await LaunchAsync(client);

        // La petición ya terminó: la campaña está en `sending` y nadie ha entregado nada.
        var accepted = await CampaignTestClient.GetAsync(client, campaign.Id);
        accepted.State.ShouldBe(CampaignState.Sending);
        accepted.SentAt.ShouldBeNull();

        _factory.Gateway.Release();

        await WaitUntilSentAsync(client, campaign.Id);

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.HandedOff);

        _factory.Gateway.Tokens.ShouldNotBeEmpty();
        _factory.Gateway.Tokens.ShouldAllBe(token => !token.IsCancellationRequested);
    }

    /// <summary>
    /// Lo que cierra una campaña es que no le quede ninguna fila <c>pending</c>, y nada más. Una
    /// lista cuyo resto son supresiones llega igual a <c>sent</c>, sin que el transporte tenga que
    /// decir nada sobre quien nunca le llegó.
    /// </summary>
    [Fact]
    public async Task Launch_ReturnsASentCampaign_WhenTheRemainderIsSuppressed()
    {
        _factory.Crm.ServeContacts(Ana, Luis);

        var client = _factory.CreateClient();

        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);
        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withRecipients = await CampaignTestClient.GetAsync(client, campaign.Id);

        // Ana se dio de baja entre la selección y el lanzamiento: su fila queda `suppressed` antes
        // del 202 y nunca llega al transporte.
        _factory.Crm.ServeMailability(Ana with { CanBeEmailed = false, SuppressionReason = "opted_out" }, Luis);

        var launch = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = withRecipients.RecipientCount });

        launch.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await WaitUntilSentAsync(client, campaign.Id);

        (await CampaignTestClient.RecipientSendStatusAsync(_factory, campaign.Id, Ana.Id))
            .ShouldBe(RecipientSendStatus.Suppressed);
        (await CampaignTestClient.RecipientSendStatusAsync(_factory, campaign.Id, Luis.Id))
            .ShouldBe(RecipientSendStatus.HandedOff);

        _factory.Gateway.Requests
            .Where(request => request.CampaignId == campaign.Id)
            .SelectMany(request => request.Recipients)
            .Select(recipient => recipient.Email)
            .ShouldBe([Luis.Email]);
    }

    /// <summary>
    /// Los tres valores del evento se copian en la fila dentro de la petición de lanzamiento. Es
    /// lo que hace que el worker pueda rendir después del <c>202</c> sin volver a llamar a
    /// events-service, y lo que sobrevive a un reinicio del proceso.
    /// </summary>
    [Fact]
    public async Task Launch_ReturnsTheEventValuesStampedOnTheRow_WhenLaunched()
    {
        var client = _factory.CreateClient();

        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);
        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Un borrador todavía no sabe nada del evento: los valores los trae el lanzamiento.
        (await CampaignTestClient.EventValuesAsync(_factory, campaign.Id)).Name.ShouldBeNull();

        var withRecipients = await CampaignTestClient.GetAsync(client, campaign.Id);

        (await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = withRecipients.RecipientCount })).StatusCode
            .ShouldBe(HttpStatusCode.Accepted);

        await WaitUntilSentAsync(client, campaign.Id);

        var values = await CampaignTestClient.EventValuesAsync(_factory, campaign.Id);

        values.Name.ShouldBe(EventsStub.EventName);
        values.Location.ShouldNotBeNullOrWhiteSpace();
        values.Dates.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// El lote completo, visto desde el transporte: un asunto, un cuerpo con sus marcadores sin
    /// tocar, el nombre del evento una sola vez en la bolsa compartida, y lo de cada persona en la
    /// suya (spec <c>04</c> §2).
    /// </summary>
    [Fact]
    public async Task Launch_ReturnsABatchWithTheLetterTheSharedBagAndALinkPerRecipient_WhenLaunched()
    {
        var client = _factory.CreateClient();
        var campaign = await LaunchAsync(client);

        await WaitUntilSentAsync(client, campaign.Id);

        var batch = _factory.Gateway.Requests.Single(request => request.CampaignId == campaign.Id);

        batch.EventId.ShouldBe(EventsStub.EventId);
        batch.Language.ShouldBe(EventsStub.EventLanguage);

        // La plantilla sembrada, tal cual: acá no se sustituye nada.
        batch.Subject.ShouldNotBeNullOrWhiteSpace();
        batch.BodyHtml.ShouldContain("{{nombre}}");
        batch.BodyHtml.ShouldContain("{{enlace_baja}}");

        batch.SharedData["evento"].ShouldBe(EventsStub.EventName);
        batch.SharedData.Keys.ShouldBe(["evento", "lugar", "fecha"], ignoreOrder: true);

        // Y el nombre del evento no se repite una vez por persona.
        batch.Recipients.ShouldNotBeEmpty();
        batch.Recipients.ShouldAllBe(recipient =>
            recipient.Data.Keys.Order().SequenceEqual(new[] { "enlace_baja", "enlace_registro", "nombre" }));
        batch.Recipients.ShouldAllBe(recipient =>
            recipient.Data["enlace_registro"].StartsWith(ServiceIdentityStub.PortalBaseUrl + "/publico/registro#t="));
    }

    /// <summary>
    /// De los dos tokens que salieron en el correo, la base se queda solo con su SHA-256. Un
    /// volcado no entrega ningún enlace que funcione (spec <c>06</c> §2.1).
    /// </summary>
    [Fact]
    public async Task Launch_ReturnsOnlyTheHashOfEachSentLinkStored_WhenLaunched()
    {
        var client = _factory.CreateClient();
        var campaign = await LaunchAsync(client);

        await WaitUntilSentAsync(client, campaign.Id);

        var batch = _factory.Gateway.Requests.Single(request => request.CampaignId == campaign.Id);

        foreach (var recipient in batch.Recipients)
        {
            var stored = await CampaignTestClient.RecipientTokenHashesAsync(
                _factory,
                campaign.Id,
                recipient.RecipientId);

            stored.Registration.ShouldBe(HashOfTokenIn(recipient.Data["enlace_registro"], "t"));
            stored.Unsubscribe.ShouldBe(HashOfTokenIn(recipient.Data["enlace_baja"], "u"));
        }
    }

    /// <summary>El hash del token que lleva un enlace en su fragmento.</summary>
    private static byte[] HashOfTokenIn(string link, string key)
    {
        var marker = $"#{key}=";

        link.ShouldContain(marker);

        return SHA256.HashData(Encoding.UTF8.GetBytes(
            link[(link.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..]));
    }

    /// <summary>Un borrador con lista, lanzado. Devuelve la campaña tal como quedó en el 202.</summary>
    private static async Task<CampaignResponse> LaunchAsync(HttpClient client)
    {
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withRecipients = await CampaignTestClient.GetAsync(client, campaign.Id);
        withRecipients.RecipientCount.ShouldBeGreaterThan(0);

        var launch = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = withRecipients.RecipientCount });

        launch.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        return withRecipients;
    }

    /// <summary>
    /// Sondea la campaña hasta que lee <c>sent</c>, que es exactamente lo que hace la pantalla
    /// (spec <c>04</c> §1.2): el <c>202</c> dice aceptada, no terminada.
    /// </summary>
    private async Task<CampaignResponse> WaitUntilSentAsync(HttpClient client, Guid campaignId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var campaign = await CampaignTestClient.GetAsync(client, campaignId);

            if (campaign.State == CampaignState.Sent)
                return campaign;

            await Task.Delay(100);
        }

        throw new TimeoutException($"La campaña {campaignId} no llegó a '{CampaignState.Sent}'.");
    }
}
