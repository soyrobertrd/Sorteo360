namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Cómo terminó el aviso por correo de un momento del intento. No es el estado del
/// envío en notification-service —eso lo cuenta el historial de aquel servicio—, sino
/// lo que esta cotización alcanzó a saber cuando lo pidió: se mandó, no había a quién
/// mandárselo, o falló.
///
/// Una falla de correo nunca tumba la operación de negocio (ADR-0020), así que este
/// valor es el único rastro de que el aviso se perdió: sin él, una cotización rechazada
/// que nadie recibió se ve igual que una que sí llegó.
/// </summary>
public static class QuoteNotificationStatus
{
    /// <summary>notification-service aceptó el envío.</summary>
    public const string Sent = "sent";

    /// <summary>No había destinatario que resolver, así que no se mandó nada.</summary>
    public const string NoRecipient = "no_recipient";

    /// <summary>Se intentó mandar y no se pudo.</summary>
    public const string Failed = "failed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Sent, NoRecipient, Failed
    };
}
