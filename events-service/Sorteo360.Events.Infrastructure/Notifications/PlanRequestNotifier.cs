using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Infrastructure.Notifications;

/// <summary>
/// Envía los avisos de solicitudes de plan y vencimiento próximo a través de
/// <see cref="INotificationClient"/> (§9.5). Un fallo de correo se registra y no bloquea la
/// operación principal (útil también en modo salón sin internet).
/// </summary>
public sealed class PlanRequestNotifier : IPlanRequestNotifier
{
    private readonly INotificationClient _notifications;
    private readonly NotificationSettings _notificationSettings;
    private readonly PlanBillingSettings _billingSettings;
    private readonly ILogger<PlanRequestNotifier> _logger;

    public PlanRequestNotifier(
        INotificationClient notifications,
        IOptions<NotificationSettings> notificationSettings,
        IOptions<PlanBillingSettings> billingSettings,
        ILogger<PlanRequestNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(notificationSettings);
        ArgumentNullException.ThrowIfNull(billingSettings);
        ArgumentNullException.ThrowIfNull(logger);
        _notifications = notifications;
        _notificationSettings = notificationSettings.Value;
        _billingSettings = billingSettings.Value;
        _logger = logger;
    }

    public async Task NotifyRequestedAsync(PlanRequest request, CancellationToken cancellationToken)
    {
        var plan = PlanCatalog.GetOrFree(request.RequestedPlan);

        if (!string.IsNullOrWhiteSpace(request.RequesterEmail))
        {
            await TrySendAsync(
                type: "events.plan-request.opened",
                toEmail: request.RequesterEmail,
                toName: request.AccountName,
                subject: $"Recibimos tu solicitud del plan {plan.DisplayName} — Sorteo360",
                body: "Hola {{cuenta}},\n\n"
                    + $"Registramos tu solicitud para activar el plan {plan.DisplayName} "
                    + $"(RD${plan.MonthlyPriceDop:N0}/mes).\n"
                    + (string.IsNullOrWhiteSpace(request.TransferReference)
                        ? string.Empty
                        : "Referencia reportada: {{referencia}}.\n")
                    + "Tu plan se activará tan pronto confirmemos la transferencia.\n\nSorteo360",
                data: DataOf(request),
                idempotencyKey: $"plan-request-opened-client-{request.Id:N}",
                entityId: request.Id,
                cancellationToken: cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_billingSettings.PlatformAdminEmail))
        {
            await TrySendAsync(
                type: "events.plan-request.admin-alert",
                toEmail: _billingSettings.PlatformAdminEmail,
                toName: "Administrador Sorteo360",
                subject: $"Nueva solicitud de plan {plan.DisplayName}: {{{{cuenta}}}}",
                body: $"La cuenta {{{{cuenta}}}} solicitó el plan {plan.DisplayName}.\n"
                    + "Referencia de transferencia: {{referencia}}.\n"
                    + $"Solicitud ID: {request.Id:D}",
                data: DataOf(request, noReference: "(sin referencia)"),
                idempotencyKey: $"plan-request-opened-admin-{request.Id:N}",
                entityId: request.Id,
                cancellationToken: cancellationToken);
        }
    }

    public async Task NotifyApprovedAsync(PlanRequest request, Account account, CancellationToken cancellationToken)
    {
        var email = request.RequesterEmail ?? account.OwnerEmail;
        if (string.IsNullOrWhiteSpace(email))
            return;

        var plan = PlanCatalog.GetOrFree(request.RequestedPlan);
        var expiresText = request.PaidPeriodEnd is { } end
            ? $" Vigente hasta el {end:yyyy-MM-dd}."
            : string.Empty;

        await TrySendAsync(
            type: "events.plan-request.approved",
            toEmail: email,
            toName: account.Name,
            subject: $"Tu plan {plan.DisplayName} ya está activo — Sorteo360",
            body: "Hola {{cuenta}},\n\n"
                + $"Confirmamos tu transferencia y activamos el plan {plan.DisplayName}.{expiresText}\n\n"
                + "Gracias por usar Sorteo360.",
            data: new(StringComparer.Ordinal) { ["cuenta"] = account.Name },
            idempotencyKey: $"plan-request-approved-{request.Id:N}",
            entityId: request.Id,
            cancellationToken: cancellationToken);
    }

    public async Task NotifyRejectedAsync(PlanRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RequesterEmail))
            return;

        var plan = PlanCatalog.GetOrFree(request.RequestedPlan);
        await TrySendAsync(
            type: "events.plan-request.rejected",
            toEmail: request.RequesterEmail,
            toName: request.AccountName,
            subject: $"Actualización sobre tu solicitud de plan {plan.DisplayName} — Sorteo360",
            body: "Hola {{cuenta}},\n\n"
                + $"No pudimos aprobar tu solicitud del plan {plan.DisplayName}.\n"
                + "Motivo: {{motivo}}\n\n"
                + "Puedes enviar una nueva solicitud cuando gustes.\n\nSorteo360",
            data: new(StringComparer.Ordinal)
            {
                ["cuenta"] = request.AccountName,
                ["motivo"] = request.RejectionReason ?? string.Empty,
            },
            idempotencyKey: $"plan-request-rejected-{request.Id:N}",
            entityId: request.Id,
            cancellationToken: cancellationToken);
    }

    public async Task NotifyExpiringSoonAsync(Account account, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.OwnerEmail) || account.PlanExpiresAt is null)
            return;

        var plan = PlanCatalog.GetOrFree(account.Plan);
        await TrySendAsync(
            type: "events.account.plan-expiring-soon",
            toEmail: account.OwnerEmail,
            toName: account.Name,
            subject: $"Tu plan {plan.DisplayName} vence el {account.PlanExpiresAt:yyyy-MM-dd} — Sorteo360",
            body: "Hola {{cuenta}},\n\n"
                + $"Tu plan {plan.DisplayName} vence el {account.PlanExpiresAt:yyyy-MM-dd}. "
                + "Si no se confirma una nueva transferencia antes de esa fecha, tu cuenta pasará "
                + "automáticamente al plan Gratis sin borrar ninguno de tus datos.\n\nSorteo360",
            data: new(StringComparer.Ordinal) { ["cuenta"] = account.Name },
            idempotencyKey: $"account-plan-expiring-{account.Id:N}-{account.PlanExpiresAt:yyyyMMdd}",
            entityId: account.Id,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lo que escribe el cliente (nombre de la cuenta, referencia, motivo) viaja como valor y no
    /// dentro del cuerpo: notification-service lo escapa al sustituirlo en el HTML.
    /// </summary>
    private static Dictionary<string, string> DataOf(PlanRequest request, string noReference = "") =>
        new(StringComparer.Ordinal)
        {
            ["cuenta"] = request.AccountName,
            ["referencia"] = request.TransferReference ?? noReference,
        };

    private async Task TrySendAsync(
        string type,
        string toEmail,
        string? toName,
        string subject,
        string body,
        Dictionary<string, string> data,
        string idempotencyKey,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = new SendNotificationRequest
            {
                Type = type,
                Language = "es",
                FromAddress = _notificationSettings.FromAddress,
                FromName = "Sorteo360",
                Subject = subject,
                Body = body,
                SharedData = data,
                Recipients = [new NotificationRecipientRequest { Email = toEmail, Name = toName }],
                About = [new NotificationAboutRequest { SourceService = "events-service", EntityType = "plan_request", EntityId = entityId.ToString("D") }],
                IdempotencyKey = idempotencyKey,
            };

            await _notifications.SendAsync(payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo enviar el correo {NotificationType} a {Email}", type, toEmail);
        }
    }
}

