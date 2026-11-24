using System.IO.Compression;
using System.Text;

namespace Sorteo360.Events.Tests.Unit.Files;

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

    /// <summary>Un PDF que pesa exactamente esos bytes: la cabecera y relleno detrás.</summary>
    public static byte[] PdfOfSize(long sizeBytes)
    {
        var content = new byte[sizeBytes];
        Pdf().CopyTo(content, 0);
        return content;
    }

    /// <summary>HEIC: contenedor ISO-BMFF con <c>ftyp</c> y la marca <c>heic</c>.</summary>
    public static byte[] Heic() =>
        [0x00, 0x00, 0x00, 0x18, .. "ftypheic"u8, 0x00, 0x00, 0x00, 0x00, .. "mif1"u8];

    /// <summary>Marcado que quien lo sube podría hacer pasar por otra cosa.</summary>
    public static byte[] Html() =>
        [.. "<html><script>alert(1)</script></html>"u8];

    /// <summary>
    /// Un libro de Excel completo: el paquete ZIP con el libro declarado en
    /// <c>[Content_Types].xml</c>. Los archivos de una empresa en el CRM lo aceptan; aquí sirve
    /// para probar que este servicio no.
    /// </summary>
    public static byte[] Xlsx()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Override PartName=\"/xl/workbook.xml\" " +
                "ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/></Types>");
            Write(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook/>");
        }

        return buffer.ToArray();
    }

    private static void Write(ZipArchive archive, string name, string text)
    {
        using var entry = archive.CreateEntry(name).Open();
        entry.Write(Encoding.UTF8.GetBytes(text));
    }
}
