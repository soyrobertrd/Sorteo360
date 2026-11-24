namespace Sorteo360.Events.Web.EventEndpoints;

public record CancelEventRequest
{
    public string? Reason { get; init; }
}
