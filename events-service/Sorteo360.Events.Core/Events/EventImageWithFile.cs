using Sorteo360.Events.Core.Files;

namespace Sorteo360.Events.Core.Events;

/// <summary>
/// Una imagen del evento junto con el archivo al que apunta: el enlace y lo que el
/// almacenamiento sabe, que son dos filas distintas y se leen juntas.
/// <para>
/// El archivo nunca falta: una imagen nace de una subida y su <c>stored_file_id</c> es
/// obligatorio. Que el almacenamiento ya no tenga los bytes es otra cosa, y se lee en
/// <c>MissingSince</c>.
/// </para>
/// </summary>
public sealed record EventImageWithFile
{
    public EventImage Image { get; init; }
    public StoredFile File { get; init; }

    public EventImageWithFile(EventImage image, StoredFile file)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(file);

        Image = image;
        File = file;
    }
}
