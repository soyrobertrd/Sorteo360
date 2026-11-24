namespace Sorteo360.Platform.Files;

/// <summary>
/// Una clase de archivo que la plataforma sabe reconocer por sus bytes iniciales, junto con lo
/// que una persona o un cliente diría de ella: cómo se declara y con qué extensión suele venir.
/// <para>
/// Que una clase esté acá no significa que se acepte: HEIC se reconoce precisamente para poder
/// rechazarlo con un motivo, en vez de con un "tipo desconocido".
/// </para>
/// </summary>
public sealed record FileSignature
{
    /// <summary>Tipo de contenido con el que se guarda y se vuelve a servir el archivo.</summary>
    public string ContentType { get; init; }

    /// <summary>
    /// Tipos que un cliente puede declarar sin contradecir el contenido. El canónico es el
    /// primero; los demás son sinónimos que circulan por ahí.
    /// </summary>
    public IReadOnlyList<string> DeclaredContentTypes { get; init; }

    /// <summary>Extensiones que no contradicen el contenido, en minúsculas y con punto.</summary>
    public IReadOnlyList<string> Extensions { get; init; }

    public FileSignature(
        string contentType,
        IReadOnlyList<string> declaredContentTypes,
        IReadOnlyList<string> extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(declaredContentTypes);
        ArgumentNullException.ThrowIfNull(extensions);

        ContentType = contentType;
        DeclaredContentTypes = declaredContentTypes;
        Extensions = extensions;
    }

    /// <summary>Indica si el tipo que declaró quien sube el archivo concuerda con el contenido.</summary>
    public bool AcceptsContentType(string declaredContentType) =>
        DeclaredContentTypes.Contains(declaredContentType, StringComparer.OrdinalIgnoreCase);

    /// <summary>Indica si la extensión del nombre concuerda con el contenido.</summary>
    public bool AcceptsExtension(string extension) =>
        Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
}
