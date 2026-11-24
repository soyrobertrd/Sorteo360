using Microsoft.AspNetCore.Mvc;

namespace Sorteo360.Convocatorias.Web.TemplateEndpoints;

/// <summary>
/// Filtros del listado de plantillas. Uno solo, y opcional.
/// </summary>
public sealed record ListTemplatesRequest
{
    /// <summary>
    /// <c>true</c> deja solo las que una campaña puede elegir. Ausente devuelve todas, incluidas
    /// las transaccionales, que es lo que ve una pantalla de administración.
    /// </summary>
    [FromQuery(Name = "selectable")]
    public bool? Selectable { get; init; }
}
