namespace Sorteo360.Events.Web.EventEndpoints;

public record MoneyResponse
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
}
