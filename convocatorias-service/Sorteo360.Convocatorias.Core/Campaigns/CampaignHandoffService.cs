using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <inheritdoc cref="ICampaignHandoffService" />
public sealed class CampaignHandoffService : ICampaignHandoffService
{
    private readonly ICampaignRepository _repository;
    private readonly ITemplateRepository _templates;
    private readonly ICampaignSendGateway _gateway;
    private readonly PortalSettings _portal;

    public CampaignHandoffService(
        ICampaignRepository repository,
        ITemplateRepository templates,
        ICampaignSendGateway gateway,
        PortalSettings portal)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(portal);

        _repository = repository;
        _templates = templates;
        _gateway = gateway;
        _portal = portal;
    }

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> HandOffAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var read = await _repository.ReadHandoffAsync(campaignId, cancellationToken);

        if (read.IsFailure)
            return UnitResult.Failure(read.Error);

        if (read.Value.HasNoValue)
            return UnitResult.Success<BaseError>();

        var handoff = read.Value.Value;

        // El id salió del canal hace un rato y entretanto pudo pasar cualquier cosa: cancelar en
        // pleno envío está permitido (spec 03 §2). Una campaña que ya no admite lotes no se
        // entrega, y no es un fallo: es que alguien la paró.
        if (!handoff.IsDeliverable)
            return UnitResult.Success<BaseError>();

        // Una fila pendiente que ya guarda sus hashes es una escritura que aterrizó a medias: su
        // correo salió con un enlace que funciona. No vuelve al transporte —sería un segundo
        // correo— y no se le acuña nada: un token nuevo pisaría el hash y mataría ese enlace.
        var alreadySent = handoff.Recipients.Where(recipient => recipient.HasTokens).ToList();

        if (alreadySent.Count > 0)
        {
            var recorded = await _repository.RecordHandoffAsync(
                campaignId,
                [.. alreadySent.Select(recipient => RecipientHandoffOutcome.Accepted(recipient.RecipientId))],
                [],
                cancellationToken);

            if (recorded.IsFailure)
                return recorded;
        }

        var pending = handoff.Recipients.Where(recipient => !recipient.HasTokens).ToList();

        if (pending.Count > 0)
        {
            // La plantilla y los valores del evento se leen una sola vez por campaña, y solo si
            // hay a quién escribirle: una campaña cuyo resto eran supresiones se cierra igual
            // aunque le falte la copia de su idioma.
            var letter = await ReadLetterAsync(handoff, cancellationToken);

            if (letter.IsFailure)
                return UnitResult.Failure(letter.Error);

            var delivered = await DeliverAsync(campaignId, letter.Value, pending, cancellationToken);

            if (delivered.IsFailure)
                return UnitResult.Failure(delivered.Error);

            // Alguien canceló entre lote y lote. La campaña no se cierra: sus filas sin entregar
            // se quedan `pending`, que es como se lee "pretendíamos escribirle y paramos".
            if (!delivered.Value)
                return UnitResult.Success<BaseError>();
        }

        return await _repository.CompleteHandoffAsync(campaignId, cancellationToken);
    }

    /// <summary>
    /// Recorre la cola por lotes de mil, acuñando los dos tokens de cada persona y anotando lo que
    /// el transporte conteste antes de pedir el lote siguiente. Devuelve si la cola se agotó:
    /// <c>false</c> es que alguien canceló en medio, y entonces no hay campaña que cerrar.
    /// </summary>
    private async Task<Result<bool, BaseError>> DeliverAsync(
        Guid campaignId,
        CampaignLetter letter,
        IReadOnlyList<PendingHandoffRecipient> pending,
        CancellationToken cancellationToken)
    {
        var first = true;

        // Se trocea con el mismo número con el que el contrato rechaza: ver
        // NotificationBatchContract.MaxRecipients.
        foreach (var batch in pending.Chunk(NotificationBatchContract.MaxRecipients))
        {
            // Y se vuelve a mirar entre lote y lote. Una lista de tres mil son tres llamadas con
            // su ida y vuelta cada una, así que mirar solo al empezar dejaría salir dos mil
            // correos de una campaña que alguien paró después del primer lote: exactamente lo que
            // permitir cancelar en pleno envío existe para evitar.
            if (!first)
            {
                var stillDeliverable = await StillDeliverableAsync(campaignId, cancellationToken);

                if (stillDeliverable.IsFailure)
                    return stillDeliverable;

                if (!stillDeliverable.Value)
                    return Result.Success<bool, BaseError>(false);
            }

            first = false;

            var minted = batch.Select(recipient => Mint(recipient, letter.EventId is not null)).ToList();

            var sent = await _gateway.SendAsync(letter.Batch(campaignId, minted), cancellationToken);

            // El lote entero se cayó. Sus filas se quedan `pending` y la campaña en `sending`:
            // la cola que queda es exactamente lo que reanuda una persona, sin escribirle dos
            // veces a quien ya recibió (spec 04 §5). Sus tokens mueren acá sin haberse guardado,
            // que es lo que deja al siguiente intento acuñar unos nuevos.
            if (sent.IsFailure)
                return Result.Failure<bool, BaseError>(sent.Error);

            // Se anota lote a lote, no al final. Un corte después del tercero deja los tres
            // anotados en vez de perder lo que el transporte ya aceptó. Los hashes van en esta
            // misma escritura: ver RecipientTokenHashes.
            var recorded = await _repository.RecordHandoffAsync(
                campaignId,
                sent.Value.Outcomes,
                [.. minted.Select(recipient => recipient.Hashes)],
                cancellationToken);

            if (recorded.IsFailure)
                return Result.Failure<bool, BaseError>(recorded.Error);
        }

        return Result.Success<bool, BaseError>(true);
    }

    /// <summary>
    /// El correo de la campaña: el asunto y el cuerpo de su plantilla, con los marcadores sin
    /// tocar, y su bolsa compartida: los tres valores del evento, o el asunto y el texto de un
    /// mensaje directo.
    /// <para>
    /// Los fallos posibles dejan las filas <c>pending</c> a propósito. Que falte la variante
    /// de un idioma no se resuelve cayendo al español (spec <c>02</c> §3), y que falten los
    /// valores del evento solo pasa en una campaña lanzada antes de que existieran las columnas:
    /// las dos son cosas que arregla una persona, no un correo con un hueco.
    /// </para>
    /// </summary>
    private async Task<Result<CampaignLetter, BaseError>> ReadLetterAsync(
        CampaignHandoff handoff,
        CancellationToken cancellationToken)
    {
        var sharedData = SharedData(handoff);

        if (sharedData is null)
        {
            return Result.Failure<CampaignLetter, BaseError>(new CampaignNotRenderableError(handoff.EventId is null
                ? "la fila no guarda el asunto y el texto del mensaje directo"
                : "la fila no guarda los valores del evento"));
        }

        var template = await _templates.GetAsync(handoff.TemplateKey, handoff.Language, cancellationToken);

        if (template.IsFailure)
            return Result.Failure<CampaignLetter, BaseError>(template.Error);

        if (template.Value.HasNoValue)
        {
            return Result.Failure<CampaignLetter, BaseError>(new CampaignNotRenderableError(
                $"no hay plantilla '{handoff.TemplateKey}' en '{handoff.Language}'"));
        }

        return Result.Success<CampaignLetter, BaseError>(
            new CampaignLetter(handoff.EventId, handoff.Language, template.Value.Value, sharedData, handoff.SenderEmail));
    }

    /// <summary>
    /// La bolsa compartida del lote, o <c>null</c> si la fila no guarda con qué llenarla. Sin
    /// evento son el asunto y el texto del mensaje directo; con evento, sus tres valores.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? SharedData(CampaignHandoff handoff)
    {
        if (handoff.EventId is not null)
            return handoff.EventValues?.ToSharedData();

        return handoff.Subject is null || handoff.Message is null
            ? null
            : CampaignDirectMessage.ToSharedData(handoff.Subject, handoff.Message);
    }

    /// <summary>
    /// Los dos tokens de una persona, y la bolsa de valores que va con ellos. El texto plano vive
    /// lo que dura la llamada al transporte: entra en los dos enlaces y no se guarda (spec
    /// <c>06</c> §2.1).
    /// <para>
    /// Un mensaje directo solo lleva el de baja: no hay evento al que inscribirse, así que no hay
    /// enlace de registro que mandar ni hash que guardar.
    /// </para>
    /// </summary>
    private MintedRecipient Mint(PendingHandoffRecipient recipient, bool hasEvent)
    {
        var registration = hasEvent ? PortalToken.Mint() : null;
        var unsubscribe = PortalToken.Mint();

        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Una fila importada que solo trae dirección recibe el saludo neutro. Saltársela se
            // descartó: dejaría fuera, en silencio, a gente que la coordinadora cree invitada
            // (spec 02 §2.2).
            [SupportedPlaceholder.RecipientName] = string.IsNullOrWhiteSpace(recipient.FirstName)
                ? SupportedPlaceholder.NeutralRecipientName
                : recipient.FirstName.Trim(),
        };

        if (registration is not null)
            data[SupportedPlaceholder.RegistrationLink] = PortalLink.Registration(_portal.BaseUrl, registration.Plaintext);

        data[SupportedPlaceholder.UnsubscribeLink] = PortalLink.Unsubscribe(_portal.BaseUrl, unsubscribe.Plaintext);

        return new MintedRecipient(
            new SendBatchRecipient(
                recipient.RecipientId,
                recipient.Email,
                RecipientDisplayName.From(recipient.FirstName, recipient.LastName),
                data),
            new RecipientTokenHashes(recipient.RecipientId, registration?.Hash, unsubscribe.Hash));
    }

    /// <summary>
    /// Si de la campaña todavía puede salir un lote, con el mismo criterio de
    /// <see cref="CampaignHandoff.IsDeliverable"/>. Un id que ya no existe cuenta como que no: lo
    /// que no está no se manda.
    /// </summary>
    private async Task<Result<bool, BaseError>> StillDeliverableAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var state = await _repository.ReadStateAsync(campaignId, cancellationToken);

        if (state.IsFailure)
            return Result.Failure<bool, BaseError>(state.Error);

        return Result.Success<bool, BaseError>(
            state.Value.HasValue && state.Value.Value is CampaignState.Sending or CampaignState.Sent);
    }

    /// <summary>Lo que no cambia entre lotes: el correo y lo que vale para todo el mundo.</summary>
    private sealed record CampaignLetter(
        Guid? EventId,
        string Language,
        Template Template,
        IReadOnlyDictionary<string, string> SharedData,
        string? FromAddress)
    {
        /// <summary>
        /// Un lote listo para el transporte. El asunto y el cuerpo van con sus marcadores
        /// intactos: sustituir es trabajo del lado que recibe (spec <c>04</c> §2).
        /// </summary>
        public SendBatchRequest Batch(Guid campaignId, IReadOnlyList<MintedRecipient> minted) =>
            new(
                campaignId,
                EventId,
                Language,
                Template.Subject,
                Template.BodyHtml,
                SharedData,
                [.. minted.Select(recipient => recipient.Recipient)],
                FromAddress);
    }

    /// <summary>Una persona con sus tokens ya acuñados: lo que sale al cable y lo que se guardará.</summary>
    private sealed record MintedRecipient(SendBatchRecipient Recipient, RecipientTokenHashes Hashes);
}
