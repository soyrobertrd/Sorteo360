namespace Sorteo360.Events.Core.Events;

public static class EventStatus
{
    public const string Planning   = "planning";
    public const string InProgress = "in_progress";
    public const string Closed     = "closed";
    public const string Cancelled  = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Planning, InProgress, Closed, Cancelled
    };

    // Estados "activos": el evento aún no terminó (ni cerrado ni cancelado).
    public static readonly IReadOnlySet<string> Active = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Planning, InProgress
    };
}
