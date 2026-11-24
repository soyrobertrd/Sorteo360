using System.Text;
using MimeKit.Text;

namespace Sorteo360.Notifications.Core.Rendering;

/// <summary>
/// Deriva la alternativa <c>text/plain</c> del cuerpo HTML ya sustituido.
/// <para>
/// El HTML lo parsea el tokenizador de MimeKit, el mismo componente que participa en el armado
/// del mensaje: acá no se interpreta marcado a mano ni con expresiones regulares. Lo único que se
/// decide es qué tokens producen texto y dónde va un salto de línea, porque nadie escribe la
/// copia dos veces: hay un solo cuerpo y esta parte se deriva de él.
/// </para>
/// </summary>
internal static class HtmlTextAlternative
{
    /// <summary>Etiquetas cuyo contenido no es copia visible y no entra en el texto.</summary>
    private static readonly HashSet<HtmlTagId> SilentTags =
    [
        HtmlTagId.Head, HtmlTagId.Title, HtmlTagId.Script, HtmlTagId.Style
    ];

    /// <summary>Etiquetas de bloque: cierran la línea en curso.</summary>
    private static readonly HashSet<HtmlTagId> BlockTags =
    [
        HtmlTagId.Address, HtmlTagId.Article, HtmlTagId.Aside, HtmlTagId.BlockQuote, HtmlTagId.Br,
        HtmlTagId.Caption, HtmlTagId.Center, HtmlTagId.Div, HtmlTagId.DD, HtmlTagId.DL, HtmlTagId.DT,
        HtmlTagId.FieldSet, HtmlTagId.Footer, HtmlTagId.Form, HtmlTagId.H1, HtmlTagId.H2, HtmlTagId.H3,
        HtmlTagId.H4, HtmlTagId.H5, HtmlTagId.H6, HtmlTagId.Header, HtmlTagId.HR, HtmlTagId.LI,
        HtmlTagId.Nav, HtmlTagId.OL, HtmlTagId.P, HtmlTagId.Pre, HtmlTagId.Section, HtmlTagId.Table,
        HtmlTagId.TBody, HtmlTagId.TD, HtmlTagId.Tfoot, HtmlTagId.TH, HtmlTagId.THead, HtmlTagId.TR,
        HtmlTagId.UL
    ];

    /// <summary>
    /// Convierte el HTML en texto: sin etiquetas, con las entidades ya resueltas por el
    /// tokenizador y con los bloques separados por saltos de línea.
    /// </summary>
    public static string Convert(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var tokenizer = new HtmlTokenizer(new StringReader(html))
        {
            DecodeCharacterReferences = true
        };

        var text = new StringBuilder();
        var silentDepth = 0;

        while (tokenizer.ReadNextToken(out var token))
        {
            switch (token)
            {
                case HtmlTagToken tag when SilentTags.Contains(tag.Id):
                    // Un <br> o un <meta> no abren nada que cerrar, así que no cuentan.
                    if (tag.IsEmptyElement)
                        break;

                    if (tag.IsEndTag)
                        silentDepth = Math.Max(0, silentDepth - 1);
                    else
                        silentDepth++;

                    break;

                case HtmlTagToken tag when BlockTags.Contains(tag.Id):
                    AppendLineBreak(text);
                    break;

                case HtmlDataToken data when silentDepth == 0:
                    AppendText(text, data.Data);
                    break;
            }
        }

        return Normalize(text);
    }

    /// <summary>
    /// Agrega el texto de un token colapsando los espacios en blanco, que en HTML no son
    /// significativos: un cuerpo indentado produciría una sangría distinta en cada línea.
    /// </summary>
    private static void AppendText(StringBuilder text, string data)
    {
        foreach (var character in data)
        {
            if (character is '\r' or '\n' or '\t' or ' ')
            {
                var last = text.Length > 0 ? text[^1] : '\n';

                if (last is not (' ' or '\n'))
                    text.Append(' ');

                continue;
            }

            text.Append(character);
        }
    }

    /// <summary>
    /// Cierra la línea en curso. No acumula líneas vacías: el marcado de un correo anida
    /// tablas y divs, y cada cierre agregaría una.
    /// </summary>
    private static void AppendLineBreak(StringBuilder text)
    {
        while (text.Length > 0 && text[^1] == ' ')
            text.Length--;

        if (text.Length > 0 && text[^1] != '\n')
            text.Append('\n');
    }

    /// <summary>Quita los espacios y saltos sobrantes de los extremos.</summary>
    private static string Normalize(StringBuilder text) =>
        text.ToString().Trim();
}
