namespace Sorteo360.Events.Web.Filters;

/// <summary>
/// La escalera de degradación del CRM, en español, para OpenAPI. Vive junto al filtro que emite las
/// cabeceras y no dentro de un <c>{Feature}EndpointsDescriptions</c> porque no es de ninguna
/// feature: el mismo par de cabeceras sale del directorio de OPT y del de empresas, y contarlo dos
/// veces es cómo dos rutas acaban describiendo distinto un mismo hecho.
/// </summary>
internal static class CrmDirectoryStatusDescription
{
    /// <summary>
    /// Qué significan los tres valores y por qué salen dos cabeceras. Cada lectura antepone la frase
    /// que dice *qué* campos suyos se resuelven; esto explica *cuánto* fiarse de ellos.
    /// </summary>
    public const string Ladder =
        """
        La cabecera `X-Crm-Directory-Status` dice qué tan confiable es lo que se devolvió:

        - `ok`: la copia del CRM está vigente.
        - `stale`: el CRM no respondió y se sirvió la última copia buena; los nombres son
          reales aunque puedan haber cambiado.
        - `unavailable`: nunca hubo una copia que servir y los nombres van en `null`.

        `X-Opt-Directory-Status` sale en la misma respuesta con el mismo valor. Es el nombre
        anterior, cuando el único directorio que se resolvía era el de OPT, y se mantiene para
        no romper a quien ya lo lee. Un cliente nuevo lee `X-Crm-Directory-Status`.

        El estado describe la copia, no cada fila: un id que el CRM ya no conoce en absoluto se
        devuelve en `null` con la cabecera en `ok`, porque la copia está perfectamente vigente y
        lo que falta es la empresa.

        La lectura tiene éxito en los tres casos: nada deja de leerse por un nombre para mostrar.
        """;
}
