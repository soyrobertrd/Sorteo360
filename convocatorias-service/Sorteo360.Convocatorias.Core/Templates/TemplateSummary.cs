namespace Sorteo360.Convocatorias.Core.Templates;

/// <summary>
/// Una plantilla <b>sin su cuerpo</b>, que es lo que necesita el selector: un nombre y una línea
/// de asunto. Mandar mil líneas de HTML por fila a un desplegable es desperdicio (spec
/// <c>02</c> §1.2).
/// </summary>
/// <param name="TemplateKey">Clave de la plantilla.</param>
/// <param name="Language">Idioma de esta variante.</param>
/// <param name="Purpose">Etiqueta: <c>campaign</c> o <c>transactional</c>.</param>
/// <param name="IsCampaignSelectable">Si aparece en el selector de una campaña.</param>
/// <param name="Subject">Asunto, todavía con sus marcadores.</param>
public sealed record TemplateSummary(
    string TemplateKey,
    string Language,
    string Purpose,
    bool IsCampaignSelectable,
    string Subject);
