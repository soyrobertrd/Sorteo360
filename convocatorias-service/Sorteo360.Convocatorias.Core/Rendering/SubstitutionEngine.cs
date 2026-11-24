using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Sorteo360.Convocatorias.Core.Rendering;

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
    public RenderedTemplate Render(string subject, string bodyHtml, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(bodyHtml);
        ArgumentNullException.ThrowIfNull(values);

        var resolved = WithNeutralGreeting(values);

        // El cuerpo es marcado: cada valor se escapa.
        var body = PlaceholderScanner.Substitute(bodyHtml, resolved, Encoder.Encode);

        // El asunto lo sustituye este mismo motor —una plantilla es asunto y cuerpo, no solo
        // cuerpo—, pero no se escapa: es una cabecera y no lo lee ningún parser de HTML, así
        // que escaparlo mostraría "Feria &amp; Turismo" en la bandeja de entrada. Lo único que
        // se le quita son los saltos de línea, que no pertenecen a un asunto y sí sirven para
        // colar otra cabecera.
        var renderedSubject = PlaceholderScanner.Substitute(subject, resolved, StripLineBreaks);

        return new RenderedTemplate(renderedSubject, body);
    }

    /// <summary>
    /// Rellena <c>{{nombre}}</c> con el saludo neutro cuando la fila importada no trae uno.
    /// Vive acá y no en quien llama para que no se pueda olvidar: es la única caída de grano
    /// de persona que existe (spec <c>02</c> §2.2).
    /// </summary>
    private static IReadOnlyDictionary<string, string> WithNeutralGreeting(
        IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue(SupportedPlaceholder.RecipientName, out var nombre)
            && !string.IsNullOrWhiteSpace(nombre))
        {
            return values;
        }

        return new Dictionary<string, string>(values, StringComparer.Ordinal)
        {
            [SupportedPlaceholder.RecipientName] = SupportedPlaceholder.NeutralRecipientName
        };
    }

    /// <summary>
    /// Deja el valor en una sola línea. Un asunto no tiene saltos de línea legítimos, y con ellos
    /// un valor podría terminar una cabecera y empezar otra.
    /// </summary>
    private static string StripLineBreaks(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal);
}
