using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <inheritdoc cref="ICancellationNoticeService" />
/// <remarks>
/// <b>Reclamar, mandar, y soltar si no salió.</b> Cada fila se sella <b>antes</b> de mandarle el
/// correo, con un <c>UPDATE</c> condicionado a que siga sin sello: dos pulsaciones simultáneas no
/// pueden reclamar la misma fila, así que nadie recibe el aviso dos veces. Si el envío falla, el
/// sello se quita y la fila vuelve a donde la ve el cubo de <c>07</c> §6.5. Lo que esto acepta, y
/// se dice en vez de esconderse: un corte entre el sello y el envío deja una fila sellada sin correo.
/// <para>
/// <b>Sin comprobación de supresión</b>, el único sitio donde la regla de <c>04</c> §3 no aplica:
/// quien se dio de baja del marketing igual tiene que enterarse de que el evento al que se inscribió
/// no va. El aviso es un deber, no una invitación. Por eso este camino no le pregunta nada al CRM.
/// </para>
/// </remarks>
public sealed class CancellationNoticeService : ICancellationNoticeService
{
    /// <summary>
    /// La etiqueta del tipo, tal cual se guarda en el historial. Es historial y nada más: no
    /// valida, no enruta y no elige plantilla (spec <c>04</c> §2).
    /// </summary>
    public const string NotificationType = "convocatorias.event.cancelled";

    private readonly IEventDirectory _events;
    private readonly ITemplateRepository _templates;
    private readonly IRegistrationRepository _registrations;
    private readonly ITransactionalNotifier _notifier;

    public CancellationNoticeService(
        IEventDirectory events,
        ITemplateRepository templates,
        IRegistrationRepository registrations,
        ITransactionalNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(notifier);

        _events = events;
        _templates = templates;
        _registrations = registrations;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<Result<CancellationNoticeOutcome, BaseError>> NotifyAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var read = await _events.GetEventAsync(eventId, cancellationToken);

        if (read.IsFailure)
            return Result.Failure<CancellationNoticeOutcome, BaseError>(read.Error);

        // Lo lee de events-service y no se fía de quien llama: un diálogo con un error no puede
        // escribirle a todos los inscritos de un evento vivo (spec 06 §4.7).
        if (read.Value.HasNoValue || !read.Value.Value.IsCancelled)
            return Result.Failure<CancellationNoticeOutcome, BaseError>(new EventNotCancelledError(eventId));

        var summary = read.Value.Value;

        var template = await _templates.GetAsync(TemplateKey.EventCancelled, summary.Language, cancellationToken);

        if (template.IsFailure)
            return Result.Failure<CancellationNoticeOutcome, BaseError>(template.Error);

        if (template.Value.HasNoValue)
        {
            return Result.Failure<CancellationNoticeOutcome, BaseError>(new CancellationNoticeUnavailableError(
                $"no hay plantilla '{TemplateKey.EventCancelled}' en '{summary.Language}'"));
        }

        var awaiting = await _registrations.ListAwaitingCancellationNoticeAsync(eventId, cancellationToken);

        if (awaiting.IsFailure)
            return Result.Failure<CancellationNoticeOutcome, BaseError>(awaiting.Error);

        var letter = template.Value.Value;
        var sharedData = EventPlaceholderValues.From(summary);
        var notified = 0;
        var failed = 0;

        foreach (var registration in awaiting.Value)
        {
            var claim = await _registrations.ClaimCancellationNoticeAsync(registration.Id, cancellationToken);

            if (claim.IsFailure)
                return Result.Failure<CancellationNoticeOutcome, BaseError>(claim.Error);

            // Otra pasada la reclamó entre la lectura y acá: el correo es suyo.
            if (!claim.Value)
                continue;

            var notification = new TransactionalNotification(
                NotificationType,
                summary.Language,
                summary.Id,
                NotificationIdempotencyKey.ForCancellationNotice(summary.Id, registration.Id),
                letter.Subject,
                letter.BodyHtml,
                sharedData,
                registration.Email,
                RecipientDisplayName.From(registration.FirstName, registration.LastName),
                RecipientData(registration));

            UnitResult<BaseError> sent;

            try
            {
                sent = await _notifier.SendAsync(notification, cancellationToken);
            }
            catch
            {
                // Un transporte que revienta tampoco puede dejar sellada a quien no recibió nada.
                await _registrations.ReleaseCancellationNoticeAsync(registration.Id, CancellationToken.None);
                throw;
            }

            if (sent.IsSuccess)
            {
                notified++;
                continue;
            }

            // Se suelta sin el token de la petición: un corte no puede dejar sellada a una persona que
            // no recibió nada. Si ni eso se pudo, la pasada se corta con el error en vez de seguir:
            // esa fila queda sellada sin correo y fuera del cubo de 07 §6.5, y quien llama tiene que
            // enterarse.
            var released = await _registrations.ReleaseCancellationNoticeAsync(registration.Id, CancellationToken.None);

            if (released.IsFailure)
                return Result.Failure<CancellationNoticeOutcome, BaseError>(released.Error);

            failed++;
        }

        return new CancellationNoticeOutcome(notified, failed);
    }

    /// <summary>
    /// La bolsa de la persona: su nombre y la referencia, para que una respuesta confundida se pueda
    /// rastrear. <b>Ni el motivo de la cancelación ni enlace alguno</b>: el motivo real suele ser
    /// "la otra ciudad vendió más", y un correo transaccional no lleva enlace de baja (spec
    /// <c>06</c> §4.7).
    /// </summary>
    private static Dictionary<string, string> RecipientData(Registration registration) =>
        new(StringComparer.Ordinal)
        {
            [SupportedPlaceholder.RecipientName] = registration.FirstName,
            [SupportedPlaceholder.Reference]     = registration.Reference,
        };
}
