namespace Sorteo360.Notifications.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia de la tabla <c>notifications.notification_suppression</c>: una
/// dirección que el proveedor reportó muerta u hostil.
/// <para>
/// La escriben los eventos de baja y de spam del webhook; el worker solo la lee, antes de abrir el
/// socket. La dirección se guarda en minúsculas porque el buzón no distingue mayúsculas para esto
/// y una supresión que se pudiera esquivar cambiando una letra no sería una supresión.
/// </para>
/// </summary>
internal sealed class NotificationSuppressionData
{
    public string Email { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
