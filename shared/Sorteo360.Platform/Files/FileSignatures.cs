using System.Text;

namespace Sorteo360.Platform.Files;

/// <summary>
/// Reconoce la clase de un archivo leyendo sus primeros bytes.
/// <para>
/// Lo que un cliente declara —el <c>Content-Type</c> del formulario y la extensión del nombre—
/// lo elige quien sube el archivo, así que no es evidencia de nada. Un documento que dice ser un
/// PDF y en realidad es marcado se convierte en script guardado en cuanto se vuelve a servir.
/// </para>
/// </summary>
public static class FileSignatures
{
    /// <summary>
    /// Bytes que hay que leer para decidir. Doce alcanzan para el más largo de los formatos
    /// reconocidos (WEBP y HEIC llevan su marca en el noveno byte); se leen dieciséis para no
    /// tener que revisar esta cuenta al agregar el próximo.
    /// </summary>
    public const int HeaderLength = 16;

    /// <summary>
    /// Lo que se registra cuando quien sube el archivo no declara nada. No contradice a ningún
    /// contenido, porque no afirma nada sobre él.
    /// </summary>
    public const string UndeclaredContentType = "application/octet-stream";

    public static FileSignature Jpeg { get; } = new(
        "image/jpeg",
        ["image/jpeg", "image/jpg", "image/pjpeg"],
        [".jpg", ".jpeg", ".jpe"]);

    public static FileSignature Png { get; } = new(
        "image/png",
        ["image/png"],
        [".png"]);

    public static FileSignature Webp { get; } = new(
        "image/webp",
        ["image/webp"],
        [".webp"]);

    public static FileSignature Pdf { get; } = new(
        "application/pdf",
        ["application/pdf", "application/x-pdf"],
        [".pdf"]);

    /// <summary>
    /// Lo que dispara un iPhone por omisión. Se reconoce pero no se acepta: Chrome y Firefox no
    /// saben mostrarlo, así que guardarlo dejaría archivos que la mitad de la gente ve rotos.
    /// </summary>
    public static FileSignature Heic { get; } = new(
        "image/heic",
        ["image/heic", "image/heif"],
        [".heic", ".heif"]);

    /// <summary>
    /// Un libro de Excel (.xlsx). No está en <see cref="Accepted"/>: lo suma quien lo pide para un
    /// tipo de subida, y no se reconoce por el encabezado, que es el de cualquier ZIP, sino por lo
    /// que hay dentro del paquete (<see cref="SpreadsheetPackage"/>).
    /// </summary>
    public static FileSignature Xlsx { get; } = new(
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
        [".xlsx"]);

    /// <summary>Las clases que se guardan. HEIC no está, a propósito.</summary>
    public static IReadOnlyList<FileSignature> Accepted { get; } = [Jpeg, Png, Webp, Pdf];

    /// <summary>
    /// Las clases que además se muestran. Es un subconjunto de <see cref="Accepted"/> para lo que
    /// se sube para verse —la portada de un evento, su galería— y no para descargarse.
    /// </summary>
    public static IReadOnlyList<FileSignature> AcceptedImages { get; } = [Jpeg, Png, Webp];

    private static ReadOnlySpan<byte> JpegMagic => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> PdfMagic => "%PDF-"u8;
    private static ReadOnlySpan<byte> RiffMagic => "RIFF"u8;
    private static ReadOnlySpan<byte> WebpMagic => "WEBP"u8;
    private static ReadOnlySpan<byte> FtypMagic => "ftyp"u8;

    /// <summary>
    /// Marcas de la familia HEIF que un iPhone escribe. La marca va después de <c>ftyp</c>, así
    /// que el contenedor por sí solo no basta: un MP4 empieza igual.
    /// </summary>
    private static readonly string[] HeifBrands =
        ["heic", "heix", "hevc", "hevx", "heim", "heis", "hevm", "hevs", "mif1", "msf1"];

    /// <summary>
    /// Devuelve la clase que corresponde a esos bytes, o <c>null</c> si no es ninguna de las
    /// conocidas. Un encabezado más corto de lo que hace falta simplemente no coincide.
    /// </summary>
    public static FileSignature? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegMagic))
            return Jpeg;

        if (header.StartsWith(PngMagic))
            return Png;

        if (header.StartsWith(PdfMagic))
            return Pdf;

        if (header.Length >= 12 && header.StartsWith(RiffMagic) && header[8..12].SequenceEqual(WebpMagic))
            return Webp;

        if (header.Length >= 12 && header[4..8].SequenceEqual(FtypMagic) && IsHeifBrand(header[8..12]))
            return Heic;

        return null;
    }

    /// <summary>Indica si esos cuatro bytes son una marca de la familia HEIF.</summary>
    private static bool IsHeifBrand(ReadOnlySpan<byte> brand)
    {
        Span<char> text = stackalloc char[4];
        Encoding.ASCII.GetChars(brand, text);

        foreach (var known in HeifBrands)
        {
            if (text.SequenceEqual(known))
                return true;
        }

        return false;
    }
}
