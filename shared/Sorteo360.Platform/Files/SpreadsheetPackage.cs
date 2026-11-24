using System.IO.Compression;
using System.Xml;

namespace Sorteo360.Platform.Files;

/// <summary>
/// Reconoce un libro de Excel (.xlsx) mirando dentro del paquete. Su encabezado es el de
/// cualquier ZIP (<c>PK</c>), igual que el de un .docx, así que los primeros bytes no bastan.
/// <para>
/// No alcanza con que exista <c>xl/workbook.xml</c>: un libro con macros (.xlsm) o una plantilla
/// (.xltx) lo tienen también. Lo que decide es el tipo con que el paquete declara esa parte en
/// <c>[Content_Types].xml</c>, que es lo mismo que mira Excel para saber qué abre.
/// </para>
/// </summary>
internal static class SpreadsheetPackage
{
    private const string WorkbookPart = "xl/workbook.xml";
    private const string ContentTypesPart = "[Content_Types].xml";
    private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";

    /// <summary>
    /// Lo más que se lee de <c>[Content_Types].xml</c>. El de un libro real ocupa unos pocos KB;
    /// el tope impide que un ZIP armado a propósito obligue a descomprimir gigas.
    /// </summary>
    private const long MaxContentTypesBytes = 1024 * 1024;

    /// <summary>Cabecera local de un ZIP: <c>PK</c> y los bytes 3 y 4.</summary>
    private static ReadOnlySpan<byte> ZipMagic => [0x50, 0x4B, 0x03, 0x04];

    /// <summary>
    /// Indica si el contenido es un paquete ZIP con un libro sin macros adentro. El paquete solo
    /// se abre si el encabezado ya es el de un ZIP. Lee desde la posición actual y no la
    /// restaura: de eso se encarga quien llama.
    /// </summary>
    public static bool IsWorkbook(ReadOnlySpan<byte> header, Stream content)
    {
        if (!header.StartsWith(ZipMagic))
            return false;

        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);

            if (archive.GetEntry(WorkbookPart) is null)
                return false;

            var contentTypes = archive.GetEntry(ContentTypesPart);

            return contentTypes is not null
                && contentTypes.Length <= MaxContentTypesBytes
                && DeclaresWorkbook(contentTypes);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException or ArgumentException)
        {
            // No es un ZIP entero, o es uno roto, o su lista de tipos no es XML: en ningún caso es
            // un libro. ArgumentException va porque el stream de un formulario de ASP.NET lanza
            // ArgumentOutOfRangeException cuando el lector busca el directorio antes del principio.
            return false;
        }
    }

    /// <summary>Indica si la lista de tipos declara la parte del libro con el tipo de un .xlsx.</summary>
    private static bool DeclaresWorkbook(ZipArchiveEntry contentTypes)
    {
        using var stream = contentTypes.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxContentTypesBytes
        });

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element
                && reader.LocalName == "Override"
                && string.Equals(reader.GetAttribute("PartName"), "/" + WorkbookPart, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(reader.GetAttribute("ContentType"), WorkbookContentType, StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}
