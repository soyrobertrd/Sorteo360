namespace Sorteo360.Events.Infrastructure.Database;

internal sealed class PlanRequestData
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string RequestedPlan { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? TransferReference { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public Guid RequestedById { get; set; }
    public string? RequesterEmail { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedById { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? PaidPeriodStart { get; set; }
    public DateTimeOffset? PaidPeriodEnd { get; set; }
}

