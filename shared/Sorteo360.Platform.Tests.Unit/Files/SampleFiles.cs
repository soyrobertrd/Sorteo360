using System.IO.Compression;
using System.Text;

namespace Sorteo360.Platform.Tests.Unit.Files;

/// <summary>
/// Encabezados reales de cada formato. Son los primeros bytes y nada más: lo que se prueba es
/// que se reconozca la clase de archivo, no que se pueda decodificar la imagen.
/// </summary>
internal static class SampleFiles
{
    /// <summary>JPEG JFIF: <c>FF D8 FF</c> y el segmento APP0.</summary>
    public static byte[] Jpeg() =>
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01];

    /// <summary>PNG: los ocho bytes de firma y el inicio de la cabecera IHDR.</summary>
    public static byte[] Png() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    /// <summary>WEBP: contenedor RIFF con la marca <c>WEBP</c> en el noveno byte.</summary>
    public static byte[] Webp() =>
        [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    /// <summary>PDF: la cabecera <c>%PDF-</c> con su versión.</summary>
    public static byte[] Pdf() =>
        [.. "%PDF-1.7\n%âãÏÓ\n"u8];

    /// <summary>HEIC: contenedor ISO-BMFF con <c>ftyp</c> y la marca <c>heic</c>.</summary>
    public static byte[] Heic() =>
        [0x00, 0x00, 0x00, 0x18, .. "ftypheic"u8, 0x00, 0x00, 0x00, 0x00, .. "mif1"u8];

    /// <summary>Marcado que quien lo sube podría hacer pasar por otra cosa.</summary>
    public static byte[] Html() =>
        [.. "<html><script>alert(1)</script></html>"u8];

    /// <summary>
    /// Un libro de Excel mínimo pero completo, como lo escribe Excel: el paquete ZIP con su
    /// <c>[Content_Types].xml</c>, el libro y una hoja. A diferencia de los demás, este no es solo
    /// el encabezado, porque un .xlsx se reconoce por lo que hay dentro del paquete.
    /// </summary>
    public static byte[] Xlsx() => OfficePackage(
        ("xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"));

    /// <summary>Un libro con macros: el mismo paquete, con el tipo del libro y el proyecto VBA de un .xlsm.</summary>
    public static byte[] Xlsm() => OfficePackage(
        ("xl/workbook.xml", "application/vnd.ms-excel.sheet.macroEnabled.main+xml"),
        ("xl/vbaProject.bin", "application/vnd.ms-office.vbaProject"));

    /// <summary>Un documento de Word: el mismo contenedor que un .xlsx, sin libro adentro.</summary>
    public static byte[] Docx() => OfficePackage(
        ("word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"));

    /// <summary>Un ZIP cualquiera, sin nada de Office.</summary>
    public static byte[] Zip()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            Write(archive, "tarifas.csv", "habitacion,precio\nsencilla,100\n");
        return buffer.ToArray();
    }

    /// <summary>Un libro de Excel 97-2003: el contenedor OLE2, el mismo de un .doc viejo.</summary>
    public static byte[] Xls() =>
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    private static byte[] OfficePackage(params (string Part, string ContentType)[] parts)
    {
        var overrides = string.Concat(parts.Select(p => $"<Override PartName=\"/{p.Part}\" ContentType=\"{p.ContentType}\"/>"));

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                overrides + "</Types>");
            Write(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                $"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"{parts[0].Part}\"/>" +
                "</Relationships>");
            foreach (var (part, _) in parts)
                Write(archive, part, "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><root/>");
        }

        return buffer.ToArray();
    }

    private static void Write(ZipArchive archive, string name, string text)
    {
        using var entry = archive.CreateEntry(name).Open();
        entry.Write(Encoding.UTF8.GetBytes(text));
    }
}
