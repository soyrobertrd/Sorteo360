namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.raffle_round</c> (una ronda del sorteo).
/// </summary>
internal sealed class RaffleRoundData
{
    public Guid Id { get; set; }
    public Guid RaffleId { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid CreatedById { get; set; }
    public Guid UpdatedById { get; set; }
}
