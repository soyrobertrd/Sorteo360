namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Hasta dónde llegó el envío de una persona. Lo escribe este servicio hasta <c>Sent</c>; lo que
/// viene después lo dicta el webhook de eventos del proveedor.
/// <para>
/// <c>Failed</c> es nuestra transacción SMTP muriendo: el mensaje no llegó al proveedor.
/// <c>Sent</c> es el <c>250 Ok: queued</c>, que no es una entrega.
/// </para>
/// </summary>
public static class DeliveryStatus
{
    /// <summary>Aceptado y persistido; el worker todavía no lo intentó.</summary>
    public const string Queued = "queued";

    /// <summary>El proveedor aceptó el mensaje.</summary>
    public const string Sent = "sent";

    /// <summary>El proveedor lo entregó al buzón.</summary>
    public const string Delivered = "delivered";

    /// <summary>El buzón lo rechazó.</summary>
    public const string Bounced = "bounced";

    /// <summary>No se intentó: la dirección está en la lista de supresión.</summary>
    public const string Dropped = "dropped";

    /// <summary>El proveedor lo reintentará más tarde.</summary>
    public const string Deferred = "deferred";

    /// <summary>Nuestra transacción SMTP falló de forma terminal.</summary>
    public const string Failed = "failed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Queued, Sent, Delivered, Bounced, Dropped, Deferred, Failed
    };
}
