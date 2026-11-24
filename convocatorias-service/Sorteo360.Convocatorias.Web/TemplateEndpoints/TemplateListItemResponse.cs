namespace Sorteo360.Convocatorias.Web.TemplateEndpoints;

/// <summary>
/// Una plantilla en el listado: lo que necesita un selector. <b>Sin <c>bodyHtml</c></b>: el
/// selector muestra un nombre y una línea de asunto, y mandar mil líneas de HTML por fila a un
/// desplegable es desperdicio.
/// </summary>
public sealed record TemplateListItemResponse
{
    /// <summary>Clave de la plantilla, por ejemplo <c>registration_invite</c>.</summary>
    public required string TemplateKey { get; init; }

    /// <summary>Idioma de esta variante.</summary>
    public required string Language { get; init; }

    /// <summary>Etiqueta: <c>campaign</c> o <c>transactional</c>.</summary>
    public required string Purpose { get; init; }

    /// <summary>Si una campaña la puede elegir.</summary>
    public required bool IsCampaignSelectable { get; init; }

    /// <summary>Asunto, todavía con sus marcadores sin sustituir.</summary>
    public required string Subject { get; init; }
}
