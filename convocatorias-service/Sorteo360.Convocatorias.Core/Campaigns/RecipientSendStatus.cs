namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// El eje de una fila de <c>campaign_recipient</c>: <b>la entrega y nada más</b>. Nace en
/// <c>pending</c> y termina en uno de los otros tres (spec <c>03</c> §3).
/// <para>
/// Tres cosas quedan fuera a propósito. <c>registered</c> es la existencia de una fila de
/// registro apuntando a este destinatario, y se deriva al leer: plegarla acá perdería "le
/// escribimos y contestó". <c>opened</c> y <c>clicked</c> necesitan el webhook del proveedor y
/// nadie pidió seguimiento de aperturas. Y la entrega necesita ese mismo webhook, así que el
/// día uno una campaña sabe qué entregó y nunca qué llegó.
/// </para>
/// </summary>
public static class RecipientSendStatus
{
    /// <summary>Todavía no se entregó. Es donde nace, y donde se queda si el envío se detiene.</summary>
    public const string Pending = "pending";

    /// <summary>Entregado al proveedor. Está a dos pasos de "entregado", nunca es lo mismo.</summary>
    public const string HandedOff = "handed_off";

    /// <summary>
    /// El emisor miró a esta persona en concreto y la rechazó, con su motivo. En la práctica
    /// solo atrapa una dirección mal formada: un fallo de transporte deja la fila en
    /// <see cref="Pending"/>, no acá, porque es lo que permite reanudar en la fila 401 en vez
    /// de volver a mandarle a las primeras 400.
    /// </summary>
    public const string Failed = "failed";

    /// <summary>
    /// Se seleccionó y no se le puede escribir. Es el resultado de la segunda comprobación de
    /// bajas, la del momento de la entrega: seleccionar y entregar no son el mismo instante, y
    /// escribirle a quien se dio de baja en medio es el único fallo que ADR-0013 no deja
    /// degradar.
    /// </summary>
    public const string Suppressed = "suppressed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending, HandedOff, Failed, Suppressed
    };

    /// <summary>
    /// Una fila de la que ya no se espera nada. Que no quede ninguna <see cref="Pending"/> es
    /// exactamente lo que lleva la campaña a <c>sent</c>, y eso lo hace el worker de la spec
    /// <c>04</c> §1, nunca la petición de lanzamiento.
    /// </summary>
    public static bool IsTerminal(string status) => status is HandedOff or Failed or Suppressed;
}
