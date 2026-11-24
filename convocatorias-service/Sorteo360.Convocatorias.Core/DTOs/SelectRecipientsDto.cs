namespace Sorteo360.Convocatorias.Core.DTOs;

/// <summary>
/// El filtro con el que se construye la lista desde el CRM. Repetible en las dos dimensiones,
/// <b>O dentro de cada una y Y entre ellas</b> (spec <c>03</c> §6), que es la regla que aplica
/// el CRM y no este servicio.
/// </summary>
/// <param name="CampaignId">La campaña que recibe la lista.</param>
/// <param name="Classifications">Clasificaciones de contacto.</param>
/// <param name="CompanyCategories">Categorías de empresa.</param>
public sealed record SelectRecipientsDto(
    Guid CampaignId,
    IReadOnlyList<string> Classifications,
    IReadOnlyList<string> CompanyCategories);
