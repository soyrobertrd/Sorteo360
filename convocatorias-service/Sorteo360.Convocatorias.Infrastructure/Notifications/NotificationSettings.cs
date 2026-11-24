using System.ComponentModel.DataAnnotations;

namespace Sorteo360.Convocatorias.Infrastructure.Notifications;

/// <summary>
/// Quién firma los correos de este servicio. Sección <c>Notifications</c>, la misma que
/// <c>Notifications__Transport</c>.
/// <para>
/// <b>Solo se exige con el transporte <c>http</c></b>, y al arrancar: notification-service
/// contesta <c>400</c> a un envío sin remitente, así que un despliegue al que le falte aceptaría
/// campañas e inscripciones y no le escribiría a nadie. Con <c>stub</c> no sale nada y nadie
/// firma.
/// </para>
/// <para>
/// <b>No hay <c>Reply-To</c>, y no es un olvido</b>, igual que en events-service: una dirección
/// de respuesta que nadie mira es peor que ninguna.
/// </para>
/// </summary>
public sealed class NotificationSettings
{
    public static string SectionName => "Notifications";

    /// <summary>La dirección que firma. <c>no-reply@sorteo360.local</c>.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>El nombre visible del remitente. Opcional: sin él, el correo muestra la dirección.</summary>
    public string? FromName { get; init; }
}
