using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Web.EventEndpoints;

/// <summary>
/// Una imagen del evento, con lo que hace falta para reconocerla en pantalla y para pedir sus
/// bytes. <c>Id</c> identifica la imagen dentro del evento y es con lo que se quita;
/// <c>StoredFileId</c> es con lo que se piden los bytes a <c>/v1/files</c>. No son
/// intercambiables.
/// </summary>
public sealed record EventImageResponse
{
    public Guid Id { get; init; }
    public Guid StoredFileId { get; init; }

    /// <summary>
    /// Qué papel cumple la imagen: <c>Cover</c> la portada del evento, <c>Gallery</c> una foto
    /// más de su galería promocional.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

    /// <summary>Cuándo se subió la imagen al evento.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// El almacenamiento ya no tiene los bytes. La fila se sigue listando: el nombre y la fecha
    /// son lo que dice qué hay que restaurar.
    /// </summary>
    public bool Missing { get; init; }

    /// <summary>
    /// Proyecta una imagen y su archivo a la forma que sale por la API. Vive acá y no en el
    /// endpoint porque la usan tres: la ruta de imágenes, el detalle del evento y la portada del
    /// listado.
    /// </summary>
    public static EventImageResponse From(EventImageWithFile item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new EventImageResponse
        {
            Id           = item.Image.Id,
            StoredFileId = item.File.Id,
            Kind         = item.Image.Kind.ToString(),
            FileName     = item.File.FileName,
            ContentType  = item.File.ContentType,
            SizeBytes    = item.File.SizeBytes,
            CreatedAt    = item.Image.CreatedAt,
            Missing      = item.File.MissingSince is not null
        };
    }
}
