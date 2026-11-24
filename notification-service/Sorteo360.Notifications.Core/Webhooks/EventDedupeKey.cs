using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sorteo360.Notifications.Core.Webhooks;

/// <summary>
/// La clave de reserva con la que un evento repetido <b>sin</b> <c>sg_event_id</c> se convierte en
/// un insert que no hace nada.
/// <para>
/// El webhook es at-least-once: un lote que el proveedor no vio confirmado vuelve, y vuelve durante
/// 24 horas. Lo que hace inofensivo ese reintento es un índice único, no una comprobación previa
/// —dos lotes idénticos en vuelo al mismo tiempo se le escaparían a la comprobación.
/// </para>
/// <para>
/// Normalmente ese índice cae sobre <c>sg_event_id</c> y esta clase no interviene. Cuando el
/// identificador no viene —la documentación se cubre llamándolo único <i>"para todo evento en el
/// que se incluya"</i>— se calcula un hash de <c>(event, email, timestamp, sg_message_id)</c> en
/// vez de descartar el evento. Dos eventos genuinamente distintos que coincidieran en esos cuatro
/// campos se tratarían como uno; se prefiere eso a perder los rebotes asíncronos, que son justo los
/// que llegan sin identificador.
/// </para>
/// </summary>
public static class EventDedupeKey
{
    /// <summary>
    /// El separador del hash: el separador de unidades de ASCII (US, 0x1F). Va dentro para que dos
    /// campos contiguos no se puedan fundir en uno, que es lo que dejaría a dos eventos distintos
    /// con el mismo hash.
    /// </summary>
    private const char Separator = (char)0x1F;

    /// <summary>El hash compuesto de los cuatro campos, en hexadecimal minúsculo.</summary>
    public static string Composite(string eventType, string email, long timestamp, string? sgMessageId)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(email);

        var material = string.Join(
            Separator,
            eventType,
            email,
            timestamp.ToString(CultureInfo.InvariantCulture),
            sgMessageId ?? string.Empty);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
