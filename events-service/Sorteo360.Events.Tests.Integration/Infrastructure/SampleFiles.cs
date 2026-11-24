namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// Encabezados reales de cada formato. Son los primeros bytes y nada más: lo que sube por la API
/// tiene que ser reconocible, no decodificable.
/// </summary>
public static class SampleFiles
{
    /// <summary>JPEG JFIF: <c>FF D8 FF</c> y el segmento APP0.</summary>
    public static byte[] Jpeg() =>
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01];

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

    /// <summary>PNG: la firma de ocho bytes y el comienzo del bloque IHDR.</summary>
    public static byte[] Png() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, .. "IHDR"u8];

    /// <summary>HEIC: contenedor ISO-BMFF con <c>ftyp</c> y la marca <c>heic</c>.</summary>
    public static byte[] Heic() =>
        [0x00, 0x00, 0x00, 0x18, .. "ftypheic"u8, 0x00, 0x00, 0x00, 0x00, .. "mif1"u8];

    /// <summary>Marcado que quien lo sube podría hacer pasar por una imagen.</summary>
    public static byte[] Html() =>
        [.. "<html><script>alert(1)</script></html>"u8];
}
