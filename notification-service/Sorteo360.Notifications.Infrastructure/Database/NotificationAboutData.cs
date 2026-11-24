namespace Sorteo360.Notifications.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia de la tabla <c>notifications.notification_about</c>: una entidad de la
/// plataforma de la que trata el envío. Es lo que hace indexable "todo lo que se mandó sobre este
/// evento".
/// </summary>
internal sealed class NotificationAboutData
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string SourceService { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
}
