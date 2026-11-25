namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.raffle_participant</c> (el padrón editable de
/// participantes del sorteo antes del congelado).
/// </summary>
internal sealed class RaffleParticipantData
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? EmailNormalized { get; set; }
    public string IdentityKey { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Phone { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public bool CheckedIn { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

