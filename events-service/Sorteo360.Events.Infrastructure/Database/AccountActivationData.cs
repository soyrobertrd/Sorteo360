namespace Sorteo360.Events.Infrastructure.Database;

internal sealed class AccountActivationData
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string EmailNormalized { get; set; } = string.Empty;
    public string? RequestedPlan { get; set; }
    public string BillingPeriod { get; set; } = "monthly";
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSentAt { get; set; }
    public int ResendCountToday { get; set; }
    public DateOnly ResendDay { get; set; }
}

