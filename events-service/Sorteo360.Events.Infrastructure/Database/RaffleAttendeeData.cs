namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.raffle_attendee</c> (un asistente del sorteo tal
/// como quedó al arrancar, calificado o no).
/// </summary>
internal sealed class RaffleAttendeeData
{
    public Guid Id { get; set; }
    public Guid RaffleId { get; set; }
    public string EmailNormalized { get; set; } = string.Empty;
    public Guid? ContactId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Company { get; set; }
    public DateTimeOffset CheckedInAt { get; set; }
    public bool Qualified { get; set; }
    public string? ExclusionReason { get; set; }
    public string? ManualDecision { get; set; }
    public Guid? ManualDecidedById { get; set; }
    public DateTimeOffset? ManualDecidedAt { get; set; }
    public string? ManualNote { get; set; }
    public string? OverruledReason { get; set; }
    public DateTimeOffset? ReinstatedAt { get; set; }
    public Guid? ReinstatedById { get; set; }
}
