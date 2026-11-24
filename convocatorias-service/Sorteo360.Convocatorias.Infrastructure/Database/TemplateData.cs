namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Fila de <c>template</c>: un correo entero en un idioma. La clave es natural,
/// <c>(template_key, language)</c>, y no hay id sustituto: es un catálogo sembrado que se
/// direcciona por su clave, y un id sería una segunda identidad que nadie usa.
/// <para>
/// Seis columnas y ninguna más. No hay cuerpo de texto plano, que duplicaría la copia de cada
/// plantilla y que ningún requisito pide; no hay <c>required_placeholders</c>, porque los
/// marcadores se derivan del cuerpo; y no hay <c>arte_url</c>, porque el arte vive dentro del
/// cuerpo.
/// </para>
/// </summary>
internal sealed class TemplateData
{
    public required string TemplateKey { get; set; }

    public required string Language { get; set; }

    public required string Subject { get; set; }

    public required string BodyHtml { get; set; }

    public required string Purpose { get; set; }

    public required bool IsCampaignSelectable { get; set; }
}
