using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Notifications;

/// <summary>
/// El transporte transaccional mientras notification-service no existe: sustituye el correo y lo
/// guarda en <c>stub_notification</c>, la misma tabla del stub de campañas (spec <c>04</c> §4.1).
/// Es la implementación hermana de <see cref="StubCampaignSendGateway"/> para un solo
/// destinatario.
/// <para>
/// <b>Es una implementación real, no un doble.</b> Es el lado que sustituye, con el mismo
/// <see cref="ISubstitutionEngine"/> que ya vigila la copia sembrada, así que el correo que se
/// guarda es el que saldría. Con él la confirmación de una inscripción funciona de punta a punta
/// sin depender de que notification-service exista.
/// </para>
/// <para>
/// <b>Guarda una fila y contesta éxito.</b> El único rechazo posible es una dirección mal formada
/// (§2.1), y la de una confirmación es la que la persona escribió y ya pasó por la validación de
/// la inscripción, así que en la práctica no rechaza a nadie. La fila se guarda igual, con su
/// estado, para que se vea qué habría salido.
/// </para>
/// </summary>
public sealed class StubTransactionalNotifier : ITransactionalNotifier
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ISubstitutionEngine _engine;

    public StubTransactionalNotifier(ConvocatoriasDbContext context, ISubstitutionEngine engine)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(engine);

        _context = context;
        _engine = engine;
    }

    /// <inheritdoc />
    public async Task<UnitResult<BaseError>> SendAsync(
        TransactionalNotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var rendered = _engine.Render(notification.Subject, notification.BodyHtml, ValuesFor(notification));

        // La dirección la escribió la persona y ya pasó por la validación de la inscripción, así
        // que esto en la práctica no rechaza a nadie. Existe porque es el único desenlace que el
        // contrato admite al aceptar (§2.1).
        var accepted = EmailAddressShape.IsWellFormed(notification.RecipientEmail);

        _context.StubNotifications.Add(new StubNotificationData
        {
            Id = Guid.NewGuid(),
            BatchId = Guid.NewGuid(),
            RecipientEmail = notification.RecipientEmail,
            Type = notification.Type,
            Language = notification.Language,
            Subject = rendered.Subject,
            BodyHtml = rendered.BodyHtml,
            Status = accepted ? CampaignNotification.StatusAccepted : CampaignNotification.StatusRejected,
            FailureReason = accepted ? null : CampaignNotification.ReasonAddressInvalid,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);

        return UnitResult.Success<BaseError>();
    }

    /// <summary>
    /// Las dos bolsas en una, que es lo que sustituye. Se añade, no se pisa: una confirmación
    /// controla las dos bolsas enteras y no pone una clave en las dos, así que el <c>Add</c> no
    /// puede colisionar (spec <c>04</c> §2).
    /// </summary>
    private static Dictionary<string, string> ValuesFor(TransactionalNotification notification)
    {
        var values = new Dictionary<string, string>(notification.SharedData, StringComparer.Ordinal);

        foreach (var (key, value) in notification.RecipientData)
            values.Add(key, value);

        return values;
    }
}
