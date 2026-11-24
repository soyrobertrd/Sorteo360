using CSharpFunctionalExtensions;

namespace Sorteo360.Platform.Files;

/// <summary>
/// Decide si un archivo se admite y de qué clase es realmente, mirando su tamaño y sus bytes.
/// Lo que devuelve es lo que dicen los bytes: el tipo declarado y la extensión solo se usan para
/// detectar que contradicen al contenido, nunca para decidir.
/// <para>
/// Es la misma regla para todos los servicios que reciben archivos. Lo que cada uno agrega
/// encima —sus códigos de error, cuántos archivos acumula un dueño, si exige una imagen— se
/// queda en el servicio.
/// </para>
/// </summary>
public static class FileContentInspector
{
    /// <summary>Tamaño máximo acordado con el cliente para un archivo: 25 MB.</summary>
    public const long DefaultMaxSizeBytes = 25L * 1024 * 1024;

    /// <summary>
    /// Inspecciona el contenido sin consumirlo. Debe poder rebobinarse y se deja en la posición
    /// en que llegó, porque quien llama lo entrega después al almacenamiento.
    /// </summary>
    /// <param name="content">Contenido a inspeccionar; debe admitir búsqueda.</param>
    /// <param name="fileName">Nombre original, del que sale la extensión declarada.</param>
    /// <param name="declaredContentType">Tipo declarado por quien sube el archivo, si declaró alguno.</param>
    /// <param name="maxSizeBytes">Tamaño máximo admitido, contado desde la posición actual.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    public static Task<Result<FileSignature, FileRejection>> InspectAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        long maxSizeBytes,
        CancellationToken cancellationToken) =>
        InspectAsync(content, fileName, declaredContentType, maxSizeBytes, FileSignatures.Accepted, cancellationToken);

    /// <summary>
    /// Igual, con la lista de clases que acepta quien llama en lugar de la de la plataforma. Sirve
    /// para ensanchar un solo tipo de subida (los archivos de una empresa admiten .xlsx) sin
    /// ensanchar los demás.
    /// </summary>
    /// <param name="accepted">Las clases que se guardan para esta subida.</param>
    public static async Task<Result<FileSignature, FileRejection>> InspectAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        long maxSizeBytes,
        IReadOnlyList<FileSignature> accepted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(accepted);

        if (!content.CanSeek)
        {
            throw new ArgumentException(
                "El contenido de una subida debe poder rebobinarse: se leen sus primeros bytes " +
                "y después se entrega entero al almacenamiento.",
                nameof(content));
        }

        // El tamaño se cuenta, no se pregunta: lo que trae el formulario es lo que se midió al
        // recibirlo, y una cabecera Content-Length la escribe quien llama.
        var start = content.Position;

        if (content.Length - start > maxSizeBytes)
            return Result.Failure<FileSignature, FileRejection>(FileRejection.TooLarge);

        var header = new byte[FileSignatures.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        content.Position = start;

        var signature = FileSignatures.Detect(header.AsSpan(0, read));

        // Un ZIP solo se abre si quien llama acepta libros: el resto de las subidas no tiene por
        // qué leer un paquete que de todos modos va a rechazar.
        if (signature is null
            && accepted.Contains(FileSignatures.Xlsx)
            && SpreadsheetPackage.IsWorkbook(header.AsSpan(0, read), content))
        {
            signature = FileSignatures.Xlsx;
        }

        content.Position = start;

        if (signature is null)
            return Result.Failure<FileSignature, FileRejection>(FileRejection.TypeNotAllowed);

        if (!accepted.Contains(signature))
        {
            // Qué se guarda lo dice una sola lista. HEIC se reconoce aparte solo para poder
            // rechazarlo con un motivo que le sirva a una persona.
            return Result.Failure<FileSignature, FileRejection>(signature == FileSignatures.Heic
                ? FileRejection.HeicNotSupported
                : FileRejection.TypeNotAllowed);
        }

        if (Contradicts(signature, declaredContentType, fileName))
            return Result.Failure<FileSignature, FileRejection>(FileRejection.TypeMismatch);

        return Result.Success<FileSignature, FileRejection>(signature);
    }

    /// <summary>
    /// Indica si lo declarado contradice a lo que dicen los bytes. No declarar nada no
    /// contradice: quien no afirma no se equivoca. Contradecir sí se rechaza en vez de
    /// corregirse, porque un archivo cuyo nombre miente es el que después se sirve confiando en
    /// el nombre.
    /// </summary>
    private static bool Contradicts(FileSignature signature, string? declaredContentType, string fileName)
    {
        // El tipo puede venir con parámetros ("image/jpeg; charset=..."); solo interesa el tipo.
        var mediaType = declaredContentType?.Split(';')[0].Trim();

        if (!string.IsNullOrEmpty(mediaType)
            && !mediaType.Equals(FileSignatures.UndeclaredContentType, StringComparison.OrdinalIgnoreCase)
            && !signature.AcceptsContentType(mediaType))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName);

        return !string.IsNullOrEmpty(extension) && !signature.AcceptsExtension(extension);
    }
}
