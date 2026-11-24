namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Uno o más premios idénticos para una ronda. <see cref="Quantity"/> es cuántas filas crear, no
/// una propiedad del premio, y por eso no existe al editar.
/// </summary>
public record CreatePrizesRequest
{
    public Guid? RoundId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Value { get; init; }

    /// <summary>La empresa patrocinadora, del CRM. Todas las filas del alta la llevan.</summary>
    public Guid? SponsorCompanyId { get; init; }

    /// <summary>Uno de sus contactos vigentes. Solo con <see cref="SponsorCompanyId"/>.</summary>
    public Guid? SponsorContactId { get; init; }

    /// <summary>Nombre libre del patrocinador para sorteos sin CRM.</summary>
    public string? SponsorName { get; init; }
    public int? Quantity { get; init; }
}
