namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.prize</c> (un premio de una ronda). No hay
/// columna de cantidad: cada fila es un premio con su propio ganador. El patrocinador va por ids y
/// sin columna de nombre (ADR-0013).
/// </summary>
internal sealed class PrizeData
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Value { get; set; }
    public Guid? SponsorCompanyId { get; set; }
    public Guid? SponsorContactId { get; set; }
    public string? SponsorName { get; set; }
    public Guid? SponsorLogoFileId { get; set; }
    public Guid? ImageStoredFileId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid CreatedById { get; set; }
    public Guid UpdatedById { get; set; }
}
