namespace Sorteo360.Events.Infrastructure.Database;

internal sealed class AccountData
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Plan { get; set; } = "free";
    public string Status { get; set; } = "Active";
    public string? OwnerEmail { get; set; }
    public DateTimeOffset? PlanStartedAt { get; set; }
    public DateTimeOffset? PlanExpiresAt { get; set; }
    public DateTimeOffset? ExpiryWarningSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

