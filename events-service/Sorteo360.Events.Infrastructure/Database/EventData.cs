using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Shared;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.event</c>.
/// Solo se usa dentro de la capa de Infrastructure para interactuar con EF Core.
/// El repositorio la convierte hacia y desde las entidades de dominio.
/// </summary>
internal sealed class EventData
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; } = Account.DefaultBanreservasAccountId;
    public string? WebhookSecret { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string? SeatingLayout { get; set; }
    public string? ExternalUrl { get; set; }
    public string? Description { get; set; }
    public int? InvitedCount { get; set; }
    public Money? EstimatedBudget { get; set; }
    public Money? TotalCost { get; set; }
    public string? CancellationReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid CreatedById { get; set; }
    public Guid UpdatedById { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
