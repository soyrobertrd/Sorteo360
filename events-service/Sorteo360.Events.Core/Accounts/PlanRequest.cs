namespace Sorteo360.Events.Core.Accounts;

/// <summary>Estados de una solicitud de cambio de plan por transferencia (§9.5).</summary>
public static class PlanRequestStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { Pending, Approved, Rejected, Cancelled };
}

/// <summary>
/// Solicitud de un plan de pago por transferencia bancaria (§9.5). Solo puede existir una
/// solicitud <see cref="PlanRequestStatus.Pending"/> por cuenta a la vez.
/// </summary>
public sealed record PlanRequest(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string RequestedPlan,
    string Status,
    string? TransferReference,
    DateTimeOffset RequestedAt,
    Guid RequestedById,
    string? RequesterEmail,
    DateTimeOffset? DecidedAt,
    Guid? DecidedById,
    string? RejectionReason,
    DateTimeOffset? PaidPeriodStart,
    DateTimeOffset? PaidPeriodEnd);

