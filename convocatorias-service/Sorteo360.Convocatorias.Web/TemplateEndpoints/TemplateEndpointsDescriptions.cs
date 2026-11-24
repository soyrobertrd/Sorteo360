namespace Sorteo360.Convocatorias.Web.TemplateEndpoints;

/// <summary>Textos del documento OpenAPI para las rutas de plantillas.</summary>
internal static class TemplateEndpointsDescriptions
{
    public const string ListSummary = "Lista las plantillas de correo";

    public const string ListDescription =
        """
        Devuelve el catálogo de plantillas sin el cuerpo: clave, idioma, propósito, si es
        seleccionable y el asunto. El cuerpo no viaja acá porque un selector muestra un nombre y
        una línea de asunto.

        Con `selectable=true` quedan solo las plantillas que una campaña puede elegir. Sin el
        parámetro se devuelven todas, incluidas las transaccionales, que ninguna campaña elige.

        El catálogo lo siembra una migración de datos: en esta fase no hay creación, edición ni
        borrado de plantillas desde la API.
        """;
}
