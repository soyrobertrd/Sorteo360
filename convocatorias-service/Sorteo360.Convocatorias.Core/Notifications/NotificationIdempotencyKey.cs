using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sorteo360.Convocatorias.Core.Campaigns;

namespace Sorteo360.Convocatorias.Core.Notifications;

/// <summary>
/// Las claves de idempotencia de lo que este servicio le pide a notification-service (spec
/// <c>01</c> §3, ADR-0020).
/// <para>
/// <b>La clave nombra el envío, no la cosa sobre la que fue.</b> La acuña quien llama, sale de
/// una identidad que ya existe y nunca es aleatoria: el mismo envío repetido —el reintento ciego
/// del chasis, o un <c>POST</c> que agotó el tiempo y se vuelve a mandar— tiene que llevar la
/// misma, para que el emisor conteste con el lote que ya guardó en vez de escribirle dos veces a
/// alguien. Y un envío distinto tiene que llevar otra, porque una clave repetida con otro cuerpo
/// es un <c>409</c>, y una repetida con el mismo cuerpo se traga el correo sin avisar a nadie.
/// </para>
/// <para>
/// El prefijo <c>convocatorias:</c> no es adorno: el espacio de claves es global entre todos los
/// que llaman al emisor.
/// </para>
/// </summary>
public static class NotificationIdempotencyKey
{
    private const string Prefix = "convocatorias";

    /// <summary>
    /// La clave de un lote de invitaciones: la campaña y una huella de las personas del lote con
    /// su bolsa de valores.
    /// <para>
    /// <b>La huella incluye los tokens a propósito.</b> Reanudar vuelve a acuñar los dos tokens de
    /// cada fila pendiente, así que el segundo intento sobre la misma tanda lleva un cuerpo
    /// distinto: una clave de "campaña y número de lote" sería la misma con otro cuerpo, un
    /// <c>409</c> en cada reanudación y una campaña varada para siempre. Los tokens de una pasada
    /// son justamente la identidad de ese intento —se guardan como hash en la misma escritura que
    /// anota el desenlace—, así que el reintento del chasis, que manda los mismos bytes, lleva la
    /// misma clave, y la reanudación, que manda otros, lleva otra.
    /// </para>
    /// <para>
    /// La huella es un SHA-256: no deja leer ningún token desde la clave que guarda el historial.
    /// </para>
    /// </summary>
    public static string ForCampaignBatch(SendBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var canonical = new StringBuilder();

        // En el orden en que salen, que es el orden en que se emparejan: el mismo lote siempre se
        // escribe igual. Las claves de cada bolsa se ordenan porque un diccionario no promete orden.
        foreach (var recipient in request.Recipients)
        {
            canonical.Append(recipient.RecipientId.ToString("N")).Append('\n');

            foreach (var (key, value) in recipient.Data.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                canonical.Append(key).Append('=').Append(value).Append('\n');

            canonical.Append('\n');
        }

        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));

        return $"{Prefix}:campaign:{request.CampaignId}:batch:{digest}";
    }

    /// <summary>
    /// La clave de una confirmación de inscripción: la inscripción y el momento en que se pidió.
    /// <para>
    /// <b>El momento no es un adorno.</b> Volver a inscribirse con la misma dirección reenvía la
    /// confirmación a propósito —en la práctica significa "perdí el correo"— y ese reenvío lleva un
    /// cuerpo idéntico: con la inscripción sola, el emisor lo leería como una repetición y
    /// contestaría <c>202</c> sin mandar nada. El momento se toma una vez por petición, así que el
    /// reintento del chasis dentro de esa petición lleva la misma clave.
    /// </para>
    /// </summary>
    public static string ForRegistrationConfirmation(Guid registrationId, DateTimeOffset requestedAt) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}:registration:{registrationId}:confirmation:{requestedAt.UtcTicks}");

    /// <summary>
    /// La clave del aviso de cancelación a una inscripción: el evento que se canceló y la persona.
    /// <para>
    /// <b>Con el evento solo no alcanza</b>: el aviso sale una vez por inscrito, y una clave por
    /// evento se tragaría el de todos menos el primero. Un aviso que se soltó porque el transporte
    /// falló y se vuelve a intentar sí lleva la misma clave, y es lo que hace que un primer intento
    /// que aterrizó sin que nos enteráramos no se mande dos veces.
    /// </para>
    /// <para>
    /// <b>Lo que esto acepta:</b> el reintento vuelve a armar el cuerpo con la plantilla y el evento
    /// de ese momento. Si alguien editó la plantilla <c>event_cancelled</c> entre un intento fallido
    /// y el siguiente, la misma clave llega con otro cuerpo, el emisor contesta <c>409</c> y esa fila
    /// se suelta otra vez: queda en el cubo de pendientes y el log lo dice, no se pierde en silencio.
    /// </para>
    /// </summary>
    public static string ForCancellationNotice(Guid eventId, Guid registrationId) =>
        $"{Prefix}:event:{eventId}:cancelled:registration:{registrationId}";
}
