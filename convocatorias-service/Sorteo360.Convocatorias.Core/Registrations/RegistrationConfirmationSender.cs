using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <inheritdoc cref="IRegistrationConfirmationSender" />
/// <remarks>
/// <b>Arma y entrega, no sustituye.</b> Lee la plantilla con sus marcadores intactos, arma las dos
/// bolsas de valores y se la pasa al transporte, que es quien sustituye (spec <c>04</c> §2). Los
/// valores del evento van en la bolsa compartida y lo de la persona en la suya, sin que una clave
/// esté en las dos: la confirmación las controla enteras, así que no puede haber colisión.
/// <para>
/// <b>Nada de la campaña.</b> La bolsa de la persona lleva su nombre, la referencia, la URL del QR
/// y el enlace a la entrada, y <b>no</b> el enlace de registro ni el de baja: una confirmación no
/// invita ni ofrece darse de baja (spec <c>06</c> §4.4). El <c>about</c> es solo el evento; no hay
/// campaña que nombrar.
/// </para>
/// </remarks>
public sealed class RegistrationConfirmationSender : IRegistrationConfirmationSender
{
    /// <summary>
    /// La etiqueta del tipo, tal cual se guarda en el historial. Es historial y nada más: no
    /// valida, no enruta y no elige plantilla (spec <c>04</c> §2).
    /// </summary>
    public const string NotificationType = "convocatorias.registration.confirmation";

    private readonly ITemplateRepository _templates;
    private readonly ITransactionalNotifier _notifier;
    private readonly PortalSettings _portal;
    private readonly TimeProvider _clock;

    public RegistrationConfirmationSender(
        ITemplateRepository templates,
        ITransactionalNotifier notifier,
        PortalSettings portal,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(portal);
        ArgumentNullException.ThrowIfNull(clock);

        _templates = templates;
        _notifier = notifier;
        _portal = portal;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> SendAsync(
        Registration registration,
        EventSummary summary,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(summary);

        var template = await _templates.GetAsync(
            TemplateKey.RegistrationConfirmation,
            summary.Language,
            cancellationToken);

        if (template.IsFailure)
            return UnitResult.Failure(template.Error);

        // Que falte la variante del idioma no se resuelve cayendo al español (spec 02 §3). La
        // inscripción ya está guardada, así que esto solo deja a la persona sin correo y queda en
        // el log: nadie lo ve como respuesta HTTP.
        if (template.Value.HasNoValue)
        {
            return UnitResult.Failure<BaseError>(new RegistrationConfirmationUnavailableError(
                $"no hay plantilla '{TemplateKey.RegistrationConfirmation}' en '{summary.Language}'"));
        }

        var letter = template.Value.Value;

        var notification = new TransactionalNotification(
            NotificationType,
            summary.Language,
            summary.Id,
            // Un reenvío lleva el mismo cuerpo y tiene que salir igual: por eso la clave lleva el
            // momento de esta petición y no solo la inscripción.
            NotificationIdempotencyKey.ForRegistrationConfirmation(registration.Id, _clock.GetUtcNow()),
            letter.Subject,
            letter.BodyHtml,
            EventPlaceholderValues.From(summary),
            registration.Email,
            RecipientDisplayName.From(registration.FirstName, registration.LastName),
            RecipientData(registration));

        return await _notifier.SendAsync(notification, cancellationToken);
    }

    /// <summary>
    /// La bolsa de la persona: su nombre, la referencia en texto, la URL de la imagen del QR y el
    /// enlace a la entrada. La URL del QR y la de la entrada se arman con el mismo <c>qr_code</c>:
    /// la primera es la imagen que sirve este servicio, la segunda la página del portal (spec
    /// <c>06</c> §4.4 y §4.5).
    /// </summary>
    private Dictionary<string, string> RecipientData(Registration registration) =>
        new(StringComparer.Ordinal)
        {
            [SupportedPlaceholder.RecipientName] = registration.FirstName,
            [SupportedPlaceholder.Reference]     = registration.Reference,
            [SupportedPlaceholder.QrImageUrl]    = PortalLink.QrImage(_portal.BaseUrl, registration.QrCode),
            [SupportedPlaceholder.TicketLink]    = PortalLink.Ticket(_portal.BaseUrl, registration.QrCode),
        };
}
