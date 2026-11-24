using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.event_image</c>: el enlace entre un evento y
/// una imagen suya. <c>StoredFileId</c> es obligatorio: un archivo se referencia de una sola
/// manera. <c>Kind</c> se guarda como texto y nunca es nulo.
/// </summary>
internal sealed class EventImageData
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid StoredFileId { get; set; }
    public EventImageKind Kind { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid CreatedById { get; set; }
    public Guid UpdatedById { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
