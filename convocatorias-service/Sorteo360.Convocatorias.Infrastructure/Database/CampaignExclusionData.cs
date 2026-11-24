namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Fila de <c>campaign_exclusion</c>: alguien que entró en el filtro y quedó fuera, con su
/// motivo. Existe para contestar "seleccioné 240 y veo 227, ¿quién falta?", que es una pregunta
/// que un contador en <c>campaign</c> no contesta.
/// </summary>
internal sealed class CampaignExclusionData
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public Guid ContactId { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    /// <summary>Nula justamente cuando el motivo es <c>no_email</c>.</summary>
    public string? Email { get; set; }

    public required string Reason { get; set; }

    public DateTimeOffset ExcludedAt { get; set; }

    public CampaignData? Campaign { get; set; }
}
