namespace Sorteo360.Notifications.Core.Webhooks;

/// <summary>
/// Los siete tipos de evento a los que este servicio está suscrito, tal como los escribe el
/// proveedor.
/// <para>
/// <b>Normativo: la lista no es un filtro.</b> Un evento de un tipo que no está acá se
/// <i>guarda</i> igual y solo se ignora al derivar el estado. El proveedor agrega tipos cuando
/// quiere, y así encender la apertura o el clic algún día es un cambio de pantalla y no una
/// migración: los eventos ya estarían en la tabla desde el momento en que alguien movió el
/// interruptor.
/// </para>
/// </summary>
public static class SendGridEventTypes
{
    /// <summary>El proveedor aceptó el mensaje y lo puso en su cola.</summary>
    public const string Processed = "processed";

    /// <summary>El proveedor se negó a mandarlo (su propia supresión, normalmente).</summary>
    public const string Dropped = "dropped";

    /// <summary>Llegó al buzón.</summary>
    public const string Delivered = "delivered";

    /// <summary>El buzón lo rechazó temporalmente; el proveedor lo reintenta.</summary>
    public const string Deferred = "deferred";

    /// <summary>El buzón lo rechazó de forma definitiva.</summary>
    public const string Bounce = "bounce";

    /// <summary>La persona lo marcó como spam. Es una reacción, no un resultado de entrega.</summary>
    public const string SpamReport = "spamreport";

    /// <summary>La persona se dio de baja.</summary>
    public const string Unsubscribe = "unsubscribe";

    /// <summary>Los siete a los que la suscripción del proveedor está configurada.</summary>
    public static readonly IReadOnlySet<string> Subscribed = new HashSet<string>(StringComparer.Ordinal)
    {
        Processed, Dropped, Delivered, Deferred, Bounce, SpamReport, Unsubscribe
    };
}
