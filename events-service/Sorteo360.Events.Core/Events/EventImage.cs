namespace Sorteo360.Events.Core.Events;

/// <summary>
/// Una imagen del evento: la portada que lo encabeza o una foto de su galería promocional. Cuál
/// de las dos es lo dice <see cref="Kind"/>.
/// <para>
/// La imagen es el enlace, no los bytes; estos viven en el almacenamiento y su ficha es
/// <c>StoredFile</c>, referenciada por <see cref="StoredFileId"/>.
/// </para>
/// <para>
/// Quitar una imagen solo da de baja el enlace (<see cref="DeletedAt"/>): los bytes y su ficha
/// se quedan, que es lo que le deja a la purga de retención la decisión de cuándo se van.
/// </para>
/// </summary>
public sealed record EventImage
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public Guid StoredFileId { get; init; }

    /// <summary>
    /// Qué papel cumple la imagen. Lo elige quien sube; el tipo de los bytes no lo puede deducir.
    /// </summary>
    public EventImageKind Kind { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public Guid CreatedById { get; init; }
    public Guid UpdatedById { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }

    public EventImage(
        Guid id,
        Guid eventId,
        Guid storedFileId,
        EventImageKind kind,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        Guid createdById,
        Guid updatedById,
        DateTimeOffset? deletedAt)
    {
        Id = id;
        EventId = eventId;
        StoredFileId = storedFileId;
        Kind = kind;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        CreatedById = createdById;
        UpdatedById = updatedById;
        DeletedAt = deletedAt;
    }
}
