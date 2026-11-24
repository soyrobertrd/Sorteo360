namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Lo que el contrato de notification-service fija para el envío de una campaña, y que por eso
/// no es configuración de nadie: la etiqueta del tipo y el vocabulario con el que se contesta
/// por cada destinatario (spec <c>04</c> §2 y §2.1).
/// <para>
/// Lo usan los dos lados de la costura: el stub, que hoy contesta con él, y el cliente tipado que
/// mañana lo leerá del cable. Tenerlo en un solo sitio es lo que hace que cambiar de transporte
/// no cambie nada de este lado.
/// </para>
/// </summary>
public static class CampaignNotification
{
    /// <summary>
    /// La etiqueta del tipo, tal cual se guarda. <b>Es historial y nada más</b>: no valida, no
    /// enruta y no elige plantilla —la plantilla la manda quien llama— así que sirve para
    /// preguntarle al historial "qué invitaciones de convocatoria salieron" y para nada más.
    /// </summary>
    public const string Type = "convocatorias.campaign.invite";

    /// <summary>
    /// El emisor persistió la fila y la va a intentar. <b>No es que el proveedor la tomara</b>:
    /// detrás hay un segundo worker drenando SMTP, así que entre esto y una entrega real hay
    /// más distancia de la que el nombre sugiere.
    /// </summary>
    public const string StatusAccepted = "accepted";

    /// <summary>
    /// El emisor miró a esta persona y la rechazó. En el momento de aceptar solo existe un motivo
    /// posible, <see cref="ReasonAddressInvalid"/>, que es por qué esta rama está casi vacía.
    /// </summary>
    public const string StatusRejected = "rejected";

    /// <summary>
    /// El motivo del único rechazo que existe al aceptar: la dirección no tiene forma de dirección.
    /// Es el campo <c>reason</c> de la respuesta de la spec <c>04</c> §2.1, con su valor literal.
    /// </summary>
    public const string ReasonAddressInvalid = "RECIPIENT_ADDRESS_INVALID";

    /// <summary>
    /// Los dos desenlaces por destinatario, que es todo lo que una respuesta puede decir.
    /// <para>
    /// Comparación exacta, no laxa de mayúsculas: de acá sale el <c>IN (...)</c> de la restricción
    /// de la tabla, y Postgres compara exacto. Un comparador laxo prometería en C# algo que la base
    /// no cumple.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> AllStatuses = new HashSet<string>(StringComparer.Ordinal)
    {
        StatusAccepted, StatusRejected
    };
}
