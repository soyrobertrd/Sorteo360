using System.Text.Json;

namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Las dos cabeceras que este servicio pone por su cuenta, y cómo se escriben.
/// <para>
/// Viven acá y no dentro del transporte porque el doble de las pruebas las necesita idénticas: con
/// la forma escrita a mano en los dos sitios, el doble puede pasar mientras el transporte de verdad
/// manda otra cosa, que es exactamente el fallo que ningún test vería.
/// </para>
/// </summary>
public static class OutboundHeaders
{
    /// <summary>
    /// La cabecera con la que el proveedor acepta metadatos por SMTP.
    /// </summary>
    public const string SmtpApi = "X-SMTPAPI";

    /// <summary>
    /// Presente para quien está fuera del MITUR y ausente para quien está dentro. Quién es quién lo
    /// decide el enlace que mandó quien llama, no este servicio.
    /// </summary>
    public const string ListUnsubscribe = "List-Unsubscribe";

    /// <summary>
    /// El contenido de <see cref="SmtpApi"/>: <c>unique_args</c> con el id opaco de la fila y nada
    /// más. El presupuesto es de 10.000 bytes con un límite de 1.000 caracteres por línea, pero el
    /// motivo de que vaya vacía de lo demás no es el tamaño: es que <c>unique_args</c> <b>no se
    /// puede borrar una vez enviado</b>, así que ahí no entra ningún id de negocio ni dato personal.
    /// </summary>
    public static string UniqueArgs(Guid recipientId) =>
        JsonSerializer.Serialize(new
        {
            unique_args = new { recipient_id = recipientId.ToString() }
        });

    /// <summary>El contenido de <see cref="ListUnsubscribe"/>: el enlace entre ángulos, por RFC 2369.</summary>
    public static string UnsubscribeValue(string url) => $"<{url}>";
}
