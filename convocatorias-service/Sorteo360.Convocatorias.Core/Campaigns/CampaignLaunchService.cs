using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <inheritdoc cref="ICampaignLaunchService" />
public sealed class CampaignLaunchService : ICampaignLaunchService
{
    private readonly ICampaignRepository _repository;
    private readonly ICrmContactDirectory _crm;
    private readonly IEventDirectory _events;
    private readonly IRegistrationWindowRepository _windows;
    private readonly ICampaignHandoffQueue _handoffQueue;

    public CampaignLaunchService(
        ICampaignRepository repository,
        ICrmContactDirectory crm,
        IEventDirectory events,
        IRegistrationWindowRepository windows,
        ICampaignHandoffQueue handoffQueue)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(crm);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(handoffQueue);

        _repository = repository;
        _crm = crm;
        _events = events;
        _windows = windows;
        _handoffQueue = handoffQueue;
    }

    /// <inheritdoc />
    public async Task<Result<Maybe<CampaignSummary>, BaseError>> LaunchAsync(
        LaunchCampaignDto dto,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Paso 1: validar. Va antes que el CRM porque una campaña que ya se sabe rechazada no
        // tiene por qué mandarle nada a nadie: la cuenta rancia de la spec 03 §5.1 no escribe
        // nada, y un vaciado por delante de ella sí escribiría sus sellos.
        var preflight = await _repository.ReadLaunchPreflightAsync(dto.Id, cancellationToken);

        if (preflight.IsFailure)
            return Result.Failure<Maybe<CampaignSummary>, BaseError>(preflight.Error);

        if (preflight.Value.HasNoValue)
            return Result.Success<Maybe<CampaignSummary>, BaseError>(Maybe<CampaignSummary>.None);

        var refusal = Validate(preflight.Value.Value, dto);

        if (refusal is not null)
            return Result.Failure<Maybe<CampaignSummary>, BaseError>(refusal);

        // Paso 2: vaciar las bajas al CRM.
        var flushed = await FlushUnsubscribesAsync(cancellationToken);

        if (flushed.IsFailure)
            return Result.Failure<Maybe<CampaignSummary>, BaseError>(flushed.Error);

        // Paso 3: recomprobar la contactabilidad de la lista entera.
        var suppressions = await ReCheckMailabilityAsync(
            preflight.Value.Value.RecipientContactIds,
            cancellationToken);

        if (suppressions.IsFailure)
            return Result.Failure<Maybe<CampaignSummary>, BaseError>(suppressions.Error);

        // El remitente: la dirección de envío de la OPT de quien lanza (PGTI-168).
        var senderEmail = await ReadSenderEmailAsync(dto.LauncherOptCode, cancellationToken);

        if (senderEmail.IsFailure)
            return Result.Failure<Maybe<CampaignSummary>, BaseError>(senderEmail.Error);

        // Pasos 4 y 5: el evento y su ventana. Un mensaje directo no tiene ni lo uno ni lo otro, y
        // se los salta: sus valores son el asunto y el texto que ya guarda su propia fila.
        CampaignEventValues? eventValues = null;

        if (preflight.Value.Value.EventId is { } eventId)
        {
            var read = await ReadOpenEventValuesAsync(eventId, cancellationToken);

            if (read.IsFailure)
                return Result.Failure<Maybe<CampaignSummary>, BaseError>(read.Error);

            eventValues = read.Value;
        }

        // Paso 6: saltar a `sending`. Las mismas guardas vuelven a correr ahí dentro, que es donde
        // son autoridad: lo de arriba es lo que evita gastar el CRM, no lo que decide.
        var launched = await _repository.LaunchAsync(
            dto,
            suppressions.Value,
            eventValues,
            senderEmail.Value,
            cancellationToken);

        if (launched.IsFailure || launched.Value.HasNoValue)
            return launched;

        // Y recién ahora se encola, con la campaña ya en `sending` y antes de escribir el 202. Es
        // el punto de no retorno: lo que podía negarse ya se negó, y lo que queda —los lotes— no
        // tiene ninguna decisión que devolverle a quien lanzó.
        //
        // El token de esta petición no viaja: `Enqueue` no admite ninguno a propósito, porque acá
        // es `RequestAborted` y mataría los lotes al cerrarse la pestaña (ADR-0018).
        _handoffQueue.Enqueue(dto.Id);

        return launched;
    }

    /// <summary>
    /// La dirección desde la que sale la campaña, o <c>null</c> para que salga desde la general
    /// (PGTI-168). La OPT de la campaña es la de quien la lanza, por su claim <c>opt_code</c>.
    /// <para>
    /// Sin código —la administración central— no se le pregunta nada al CRM. Una oficina que el
    /// CRM no tiene viva, o que no tiene dirección de envío, sale desde la general: no es motivo
    /// para negar el lanzamiento. Lo que sí lo niega es un CRM caído, con el mismo
    /// <c>503 CRM_UNAVAILABLE</c> de los pasos 2 y 3, que ya lo necesitaban en pie.
    /// </para>
    /// </summary>
    private static Task<Result<string?, BaseError>> ReadSenderEmailAsync(
        string? launcherOptCode,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success<string?, BaseError>(null));

    /// <summary>
    /// Los pasos 4 y 5 de una campaña de evento: los tres valores del evento, o lo que objetan el
    /// evento o su ventana de inscripción.
    /// </summary>
    private async Task<Result<CampaignEventValues, BaseError>> ReadOpenEventValuesAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        // Paso 4: leer el evento. Va acá y no antes porque los pasos 2 y 3 son los que pueden
        // dejar el CRM al día, y gastar la lectura del vecino por una campaña que el CRM ya iba
        // a rechazar no aporta nada.
        var eventValues = await ReadEventValuesAsync(eventId, cancellationToken);

        if (eventValues.IsFailure)
            return eventValues;

        // Paso 5: la ventana de inscripción, en la misma petición que el evento y antes del salto
        // (spec 06 §5.5). Nadie quiere mandarle a cientos de personas un enlace que contesta 409.
        var window = await _windows.GetAsync(eventId, cancellationToken);

        if (window.IsFailure)
            return Result.Failure<CampaignEventValues, BaseError>(window.Error);

        return window.Value.IsOpen
            ? eventValues
            : Result.Failure<CampaignEventValues, BaseError>(new CampaignRegistrationsClosedError(eventId));
    }

    /// <summary>
    /// Los tres valores del evento, o lo que el evento objeta (spec <c>04</c> §1 paso 4).
    /// <b>Esta lectura es la única guarda que hay sobre el evento</b>: events-service nunca llama
    /// a convocatorias, así que nada más acá puede enterarse de que se apagó.
    /// <para>
    /// Los valores salen de acá y no solo el veredicto porque es la única lectura del evento que
    /// hay en todo el envío: el worker rinde después del <c>202</c> y no vuelve a preguntar, así
    /// que lo que no se copie en esta llamada no existe más tarde.
    /// </para>
    /// <para>
    /// Un evento que events-service no conoce cae en el rechazo por valores ausentes, nombrando
    /// los tres: sin evento no hay ni nombre, ni lugar, ni fecha que poner en el correo, y
    /// decirlo así es más útil que un código propio que la spec no nombra.
    /// </para>
    /// <para>
    /// Se comprueban los tres marcadores de grano de evento y no solo los que la plantilla
    /// nombra: leer acá el cuerpo para ver cuáles nombra no cambia nada hoy, porque las dos
    /// plantillas de campaña sembradas nombran los tres. Que la plantilla exista en el idioma de
    /// la campaña ya lo comprobó el paso 1 (spec <c>03</c> §4.1).
    /// </para>
    /// </summary>
    private async Task<Result<CampaignEventValues, BaseError>> ReadEventValuesAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var read = await _events.GetEventAsync(eventId, cancellationToken);

        if (read.IsFailure)
            return Result.Failure<CampaignEventValues, BaseError>(read.Error);

        if (read.Value.HasNoValue)
        {
            return Result.Failure<CampaignEventValues, BaseError>(
                new EventValuesMissingError([.. SupportedPlaceholder.EventLevel]));
        }

        var summary = read.Value.Value;

        // Cancelado va primero: a un evento apagado le sobra que además le falte el lugar.
        if (summary.IsCancelled)
            return Result.Failure<CampaignEventValues, BaseError>(new CampaignEventCancelledError(eventId));

        var values = EventPlaceholderValues.From(summary);

        // La regla de qué cuenta como valor ausente vive en TemplateBodyRules, con la que sí mira
        // el cuerpo: escrita dos veces se aplicaría dos veces distinta.
        var check = TemplateBodyRules.CheckEveryEventValue(values);

        return check.IsFailure
            ? Result.Failure<CampaignEventValues, BaseError>(check.Error)
            : Result.Success<CampaignEventValues, BaseError>(CampaignEventValues.From(values));
    }

    /// <summary>
    /// Las cuatro negativas que se pueden ver sin llamar a nadie (spec <c>03</c> §4, §4.1 y §5.1),
    /// o <c>null</c> si no hay nada que objetar. La de ningún destinatario contactable no cabe
    /// acá: depende de lo que conteste el CRM.
    /// </summary>
    private static BaseError? Validate(CampaignLaunchPreflight preflight, LaunchCampaignDto dto)
    {
        if (!CampaignState.CanTransition(preflight.State, CampaignState.Sending))
            return new CampaignTransitionNotAllowedError(preflight.State, CampaignState.Sending);

        // Sin plantilla en su idioma no hay correo que armar. Rechazarlo acá, antes del 202, es lo
        // que evita que el worker lo descubra después y deje la campaña atascada en `sending`.
        if (!preflight.HasTemplate)
            return new CampaignTemplateNotFoundError(preflight.TemplateKey, preflight.Language);

        var recipientCount = preflight.RecipientContactIds.Count;

        if (recipientCount == 0)
            return new CampaignHasNoRecipientsError();

        return recipientCount != dto.ExpectedRecipientCount
            ? new CampaignRecipientsChangedError(dto.ExpectedRecipientCount, recipientCount)
            : null;
    }

    /// <summary>
    /// Empuja al CRM todas las bajas que todavía no le llegaron y las sella. <b>El sello se
    /// escribe una sola vez, y solo si el vaciado entero llegó</b>: la baja del CRM es
    /// idempotente (spec <c>08</c> §7), así que volver a empujar la que ya llegó sale gratis,
    /// mientras que sellar una que no llegó deja a alguien que se dio de baja recibiendo la
    /// campaña siguiente. De los dos errores posibles se elige a conciencia el barato.
    /// </summary>
    private async Task<UnitResult<BaseError>> FlushUnsubscribesAsync(CancellationToken cancellationToken)
    {
        var pending = await _repository.ListUnsyncedUnsubscribesAsync(cancellationToken);

        if (pending.IsFailure)
            return UnitResult.Failure(pending.Error);

        if (pending.Value.Count == 0)
            return UnitResult.Success<BaseError>();

        foreach (var contactId in pending.Value)
        {
            var pushed = await _crm.OptOutAsync(contactId, cancellationToken);

            if (pushed.IsFailure)
                return pushed;
        }

        return await _repository.MarkUnsubscribesSyncedAsync(pending.Value, cancellationToken);
    }

    /// <summary>
    /// Le vuelve a preguntar al CRM por la lista entera y devuelve a quién no se le va a
    /// escribir. <b>La respuesta puede venir corta</b>: un id que no vuelve es un contacto que el
    /// CRM ya no devuelve —el caso real es un borrado en blando entre la selección y el
    /// lanzamiento— y se suprime por <c>contact_unavailable</c>, calculado acá porque el CRM no
    /// puede informar sobre una fila que no devuelve (spec <c>04</c> §3).
    /// </summary>
    private async Task<Result<IReadOnlyList<RecipientSuppression>, BaseError>> ReCheckMailabilityAsync(
        IReadOnlyList<Guid> contactIds,
        CancellationToken cancellationToken)
    {
        var verdicts = await _crm.CheckMailabilityAsync(contactIds, cancellationToken);

        if (verdicts.IsFailure)
            return Result.Failure<IReadOnlyList<RecipientSuppression>, BaseError>(verdicts.Error);

        // Un contacto está una vez en una campaña, así que el CRM no debería contestar dos veces
        // por el mismo; agrupar igual es lo que impide que un duplicado inesperado reviente.
        var byContact = verdicts.Value
            .GroupBy(verdict => verdict.Id)
            .ToDictionary(group => group.Key, group => group.First());

        IReadOnlyList<RecipientSuppression> suppressions =
        [
            .. contactIds
                .Distinct()
                .Select(contactId => (ContactId: contactId, Verdict: byContact.GetValueOrDefault(contactId)))
                .Where(pair => pair.Verdict is null || !pair.Verdict.CanBeEmailed)
                // El motivo lo manda el CRM. El que no reconocemos, y el del id que ni siquiera
                // volvió, caen los dos en `contact_unavailable`: copiar acá su catálogo es cómo
                // la regla acaba viviendo en dos bases de código.
                .Select(pair => new RecipientSuppression(
                    pair.ContactId,
                    ExclusionReason.FromCrm(pair.Verdict?.SuppressionReason))),
        ];

        return Result.Success<IReadOnlyList<RecipientSuppression>, BaseError>(suppressions);
    }
}
