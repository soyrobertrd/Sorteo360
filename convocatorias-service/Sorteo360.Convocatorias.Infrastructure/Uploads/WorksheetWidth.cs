using System.IO.Compression;
using System.Xml;

namespace Sorteo360.Convocatorias.Infrastructure.Uploads;

/// <summary>
/// La columna más lejana que declara o usa cualquier hoja del libro, antes de dárselo a MiniExcel.
/// <para>
/// MiniExcel arma cada fila con todas las columnas hasta la última, así que un libro de 1,6 KB con
/// una celda en <c>XFD</c> (16.384 columnas) lo tuvo más de 20 s por subida (auditoría run-1). Se
/// miran tanto <c>&lt;dimension&gt;</c> como cada <c>&lt;c r&gt;</c>, porque las dos las escribe
/// quien manda el archivo. La pasada es lineal y ya la acota el presupuesto de descompresión.
/// </para>
/// </summary>
internal static class WorksheetWidth
{
    /// <summary>Indica si ninguna hoja pasa de <paramref name="maxColumns"/>. Deja el contenido rebobinado.</summary>
    public static bool IsWithin(Stream content, int maxColumns)
    {
        var start = content.Position;

        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                    || !entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var stream = entry.Open();

                if (MaxColumn(stream, maxColumns) > maxColumns)
                    return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException)
        {
            // Un zip o un XML roto no se le da a MiniExcel: no cabe porque no se puede medir.
            return false;
        }
        finally
        {
            content.Position = start;
        }
    }

    /// <summary>La columna más alta de una hoja, o la primera que pase del tope (no hace falta seguir).</summary>
    private static int MaxColumn(Stream sheet, int maxColumns)
    {
        using var reader = XmlReader.Create(sheet, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });

        var max = 0;
        var position = 0;

        while (reader.Read() && max <= maxColumns)
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            switch (reader.LocalName)
            {
                case "dimension":
                    // "A1:XFD5": lo que importa es el extremo derecho.
                    var range = reader.GetAttribute("ref") ?? string.Empty;
                    max = Math.Max(max, ColumnOf(range[(range.LastIndexOf(':') + 1)..]));
                    break;

                case "row":
                    position = 0;
                    break;

                case "c":
                    // Una celda sin "r" va justo después de la anterior.
                    var reference = reader.GetAttribute("r");
                    position = reference is null ? position + 1 : ColumnOf(reference);
                    max = Math.Max(max, position);
                    break;
            }
        }

        return max;
    }

    /// <summary>El número de columna de una referencia como "XFD5" (A = 1). Corta en cuanto pasa de lo razonable.</summary>
    private static int ColumnOf(string reference)
    {
        var column = 0;

        foreach (var character in reference)
        {
            var upper = char.ToUpperInvariant(character);

            if (upper is < 'A' or > 'Z')
                break;

            column = Math.Min(column * 26 + (upper - 'A' + 1), 1_000_000);
        }

        return column;
    }
}
