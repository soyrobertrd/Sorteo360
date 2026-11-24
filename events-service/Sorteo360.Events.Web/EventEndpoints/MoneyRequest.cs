namespace Sorteo360.Events.Web.EventEndpoints;

public record MoneyRequest
{
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
}
