namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Una entidad de la plataforma sobre la que va el envío. Es lo que contesta "muéstrame todo lo
/// que se mandó sobre la Feria de Madrid" con un índice en vez de un LIKE.
/// <para>
/// Nunca sale de la plataforma: estos ids no viajan en las cabeceras del mensaje.
/// </para>
/// </summary>
public sealed record NotificationAbout
{
    public Guid Id { get; init; }
    public Guid BatchId { get; init; }
    public string SourceService { get; init; }
    public string EntityType { get; init; }
    public string EntityId { get; init; }

    public NotificationAbout(Guid id, Guid batchId, string sourceService, string entityType, string entityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceService);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        Id = id;
        BatchId = batchId;
        SourceService = sourceService;
        EntityType = entityType;
        EntityId = entityId;
    }
}
