using System.ComponentModel.DataAnnotations;

namespace Sorteo360.Events.Infrastructure.Notifications;

/// <summary>
/// Quién firma los correos de este servicio. Sección <c>Notifications</c>.
/// <para>
/// <b>No hay <c>Reply-To</c>, y no es un olvido.</b> Una dirección de respuesta que nadie mira es
/// peor que ninguna: sin ella, al menos el rebote le avisa a quien contestó que su respuesta no
/// llegó a ninguna parte.
/// </para>
/// </summary>
public sealed class NotificationSettings
{
    public static string SectionName => "Notifications";

    /// <summary>La dirección que firma. <c>no-reply@sorteo360.local</c>.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = string.Empty;
}
