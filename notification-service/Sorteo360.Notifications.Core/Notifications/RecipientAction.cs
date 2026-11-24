namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Lo que hizo la persona con el correo, que es un eje distinto del de la entrega: alguien puede
/// reportar spam sobre un mensaje entregado sin que la entrega deje de ser cierta. Lo escribe
/// únicamente el webhook.
/// </summary>
public static class RecipientAction
{
    /// <summary>No hizo nada, o no lo sabemos.</summary>
    public const string None = "none";

    /// <summary>Se dio de baja.</summary>
    public const string Unsubscribed = "unsubscribed";

    /// <summary>Reportó el mensaje como spam.</summary>
    public const string Complained = "complained";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        None, Unsubscribed, Complained
    };
}
