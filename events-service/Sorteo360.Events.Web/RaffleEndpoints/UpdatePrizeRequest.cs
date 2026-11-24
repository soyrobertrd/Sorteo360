namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// La representación editable completa de un premio (ADR-0009): lo opcional omitido se borra, y la
/// ronda es obligatoria porque es parte de él —cambiarla lo mueve—.
/// </summary>
public record UpdatePrizeRequest
{
    public Guid? RoundId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Value { get; init; }

    /// <summary>La empresa patrocinadora, del CRM. Omitirla quita el patrocinio.</summary>
    public Guid? SponsorCompanyId { get; init; }

    /// <summary>Uno de sus contactos vigentes. Solo con <see cref="SponsorCompanyId"/>.</summary>
    public Guid? SponsorContactId { get; init; }

    /// <summary>Nombre libre del patrocinador para sorteos sin CRM.</summary>
    public string? SponsorName { get; init; }
}
