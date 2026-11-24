namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Los dos formatos que se admiten, con lo que la inspección averiguó de cada uno.
/// <para>
/// Se reconocen de forma distinta y eso no es un detalle: un xlsx se identifica por sus bytes
/// (spec <c>05</c> §3.1) y un csv <b>no se puede identificar por sus bytes</b>, es texto sin
/// marca, así que su validación <i>es</i> una prueba de decodificar y parsear (§3.2).
/// </para>
/// </summary>
/// <param name="Kind">Cuál de los dos es.</param>
/// <param name="Encoding">
/// Con qué codificación se leyó. Para un xlsx siempre <c>utf-8</c>, que es lo que dice el
/// formato. Para un csv es el resultado de la prueba, y <b>se muestra en el resumen</b>: quien
/// vea "leído como Windows-1252" al lado de un <c>José</c> destrozado tiene por dónde empezar,
/// mientras que un respaldo silencioso escribe mojibake en el sistema de registro.
/// </param>
/// <param name="Delimiter">
/// El separador con el que el csv se parte en campos, nulo para un xlsx. Lo decide la
/// inspección y no el parser: si cada uno lo dedujera por su cuenta, un archivo raro podría
/// admitirse con un separador y leerse con otro.
/// </param>
public sealed record SpreadsheetFormat(SpreadsheetKind Kind, string Encoding, char? Delimiter = null)
{
    /// <summary>La codificación de un xlsx, que el formato fija y no hace falta adivinar.</summary>
    public const string Utf8 = "utf-8";

    /// <summary>El respaldo de §3.2 cuando la decodificación estricta en UTF-8 falla.</summary>
    public const string Windows1252 = "windows-1252";
}

/// <summary>Cuál de los dos formatos admitidos es un archivo.</summary>
public enum SpreadsheetKind
{
    Xlsx,
    Csv,
}
