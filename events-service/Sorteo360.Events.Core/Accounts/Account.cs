namespace Sorteo360.Events.Core.Accounts;

/// <summary>Estados del ciclo de vida de una cuenta cliente (§9.1, §9.3).</summary>
public static class AccountStatus
{
    public const string PendingActivation = "PendingActivation";
    public const string Active = "Active";
    public const string Suspended = "Suspended";
}

/// <summary>
/// Empresa cliente de Sorteo360 (§9.1). Guarda su plan vigente y, si es de pago, el período
/// pagado hasta el cual aplica antes de volver a <c>free</c>.
/// </summary>
public sealed record Account(
    Guid Id,
    string Name,
    string Plan,
    string Status,
    string? OwnerEmail,
    DateTimeOffset? PlanStartedAt,
    DateTimeOffset? PlanExpiresAt,
    DateTimeOffset? ExpiryWarningSentAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Cuenta por defecto sembrada para la operación actual de Banreservas (plan Avanzado).</summary>
    public static readonly Guid DefaultBanreservasAccountId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");
}

