using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Sorteo360.Notifications.Core.Rendering;

/// <inheritdoc cref="ISubstitutionEngine" />
public sealed class SubstitutionEngine : ISubstitutionEngine
{
    /// <summary>
    /// Escapa lo que tiene significado en HTML y nada más. El resto del Unicode se deja como
    /// está: el cuerpo viaja en UTF-8 y convertir cada tilde en una referencia numérica llenaría
    /// de ruido una copia que es toda en español.
    /// </summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(new TextEncoderSettings(UnicodeRanges.All));

    /// <inheritdoc />
    public RenderedEmail Render(string subject, string htmlBody, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(htmlBody);
        ArgumentNullException.ThrowIfNull(values);

        // El cuerpo es marcado: cada valor se escapa. Los valores llegan de una hoja de cálculo
        // que sube una persona, así que un "Juan <script>" o un & suelto es un camino vivo desde
        // una celda hasta el correo.
        var body = PlaceholderScanner.Substitute(htmlBody, values, Encoder.Encode);

        // El asunto es una cabecera y no lo lee ningún parser de HTML: escaparlo mostraría
        // "Feria &amp; Turismo" en la bandeja de entrada. Lo único que se le quita son los saltos
        // de línea, que no pertenecen a un asunto y sí sirven para colar otra cabecera.
        var renderedSubject = PlaceholderScanner.Substitute(subject, values, StripLineBreaks);

        return new RenderedEmail(renderedSubject, body, ToPlainText(body));
    }

    /// <summary>
    /// Deriva la alternativa de texto del HTML ya sustituido. Derivarla en vez de escribirla es
    /// lo que evita un segundo cuerpo que mantener.
    /// </summary>
    private static string ToPlainText(string html) => HtmlTextAlternative.Convert(html);

    /// <summary>
    /// Deja el valor en una sola línea. Un asunto no tiene saltos de línea legítimos, y con ellos
    /// un valor podría terminar una cabecera y empezar otra.
    /// </summary>
    private static string StripLineBreaks(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
