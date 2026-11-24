namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.raffle_attendee_override</c> (una decisión manual
/// sobre un asistente del sorteo).
/// </summary>
internal sealed class RaffleAttendeeOverrideData
{
    public Guid Id { get; set; }
    public Guid RaffleId { get; set; }
    public string EmailNormalized { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string? Note { get; set; }
    public Guid DecidedById { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}
