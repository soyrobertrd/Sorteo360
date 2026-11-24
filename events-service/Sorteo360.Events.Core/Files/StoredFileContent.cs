namespace Sorteo360.Events.Core.Files;

/// <summary>
/// Los bytes de un archivo junto con su ficha. El contenido va como <see cref="Stream"/> y no
/// como arreglo a propósito: se sirve en streaming, así que un archivo grande no pasa entero por
/// la memoria del servicio. Quien recibe este objeto es dueño del stream y debe liberarlo.
/// </summary>
public sealed record StoredFileContent
{
    public StoredFile File { get; init; }
    public Stream Content { get; init; }

    public StoredFileContent(StoredFile file, Stream content)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(content);

        File = file;
        Content = content;
    }
}
