namespace Sorteo360.Notifications.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia de la tabla <c>notifications.notification_batch</c>: un POST aceptado.
/// Solo se usa dentro de Infrastructure; el repositorio la convierte hacia y desde el dominio.
/// <para>
/// <c>Body</c> guarda el cuerpo <b>sin sustituir</b>, con sus <c>{{placeholders}}</c> intactos, y
/// no hay ninguna columna para los valores de <c>data</c> en esta tabla ni en las otras tres.
/// </para>
/// </summary>
internal sealed class NotificationBatchData
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string BodySha256 { get; set; } = string.Empty;
    public bool TrackingExpected { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
