namespace Sorteo360.Notifications.Core.DTOs.History;

/// <summary>
/// Los filtros del listado del historial. Todos opcionales salvo la paginación, que llega ya
/// resuelta desde la capa web.
/// <para>
/// <c>SentFrom</c> / <c>SentTo</c> acotan por <c>queued_at</c>, que es cuando la plataforma aceptó
/// el envío. No por <c>sent_at</c>: una fila que nunca salió no tiene <c>sent_at</c>, y esas son
/// justo las que se buscan cuando algo falló. Acotar por la otra columna las haría desaparecer del
/// rango.
/// </para>
/// </summary>
public sealed record NotificationHistoryQueryDto(
    int Page,
    int PageSize,
    string? Type = null,
    string? DeliveryStatus = null,
    string? RecipientAction = null,
    string? Email = null,
    string? EntityType = null,
    string? EntityId = null,
    string? SourceService = null,
    DateTimeOffset? SentFrom = null,
    DateTimeOffset? SentTo = null,
    IReadOnlyList<Guid>? Ids = null);
