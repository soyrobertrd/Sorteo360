using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Sorteo360.Events.Tests.Unit.Templates;

/// <summary>
/// La copia del dialecto de notification-service que usan los tests de archivo dorado. Por qué es
/// una copia y qué la protege está en <see cref="QuoteTemplateGoldenFileTests"/>.
/// </summary>
internal static class TemplateDialect
{
    /// <summary>
    /// El mismo codificador que usa notification-service: escapa lo que tiene significado en HTML
    /// y nada más. <c>WebUtility.HtmlEncode</c> no sirve acá —convierte cada tilde en una
    /// referencia numérica—, y una copia que es toda en español quedaría ilegible en el archivo
    /// dorado, que es justamente lo que alguien tiene que poder revisar de un vistazo.
    /// </summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(new TextEncoderSettings(UnicodeRanges.All));

    /// <summary>El dialecto de notification-service: <c>{{clave}}</c> plano, sin condicionales.</summary>
    public static string Substitute(string text, IReadOnlyDictionary<string, string> values, bool escapeHtml) =>
        Regex.Replace(text, @"\{\{([^{}]*)\}\}", match =>
        {
            var key = match.Groups[1].Value.Trim();

            if (!values.TryGetValue(key, out var value))
                return match.Value;

            return escapeHtml ? Encoder.Encode(value) : value;
        });

    /// <summary>
    /// Lee el archivo aprobado, normalizando los finales de línea: el repositorio se clona en Mac,
    /// Linux y Windows, y un CRLF no es un cambio de copia.
    /// </summary>
    public static string ReadGolden(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "Golden", fileName);

        return File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd();
    }
}
