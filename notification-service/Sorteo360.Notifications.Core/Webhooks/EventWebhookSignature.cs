using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sorteo360.Notifications.Core.Webhooks;

/// <summary>
/// La verificación ECDSA de un lote del webhook. Es toda la autenticación que este endpoint tiene:
/// no hay política de Keycloak, porque el proveedor no es un usuario del realm.
/// <para>
/// <b>Normativo: se verifica <c>timestamp + cuerpo crudo</c>, y recién después se parsea.</b>
/// Cualquier re-serialización —el model binding, una vuelta por JSON, normalizar espacios— rompe la
/// verificación <i>en silencio</i>: el objeto sigue siendo el mismo y los bytes ya no. Por eso este
/// método recibe el cuerpo como llegó y nunca un objeto.
/// </para>
/// <para>
/// <b>Se usa la criptografía de la BCL y no el SDK del proveedor.</b>
/// <c>SendGrid.Helpers.EventWebhook.RequestValidator</c> hace exactamente esto —P-256, SHA-256,
/// firma DER, concatenación de la marca de tiempo con la carga— y traerlo significaría arrastrar el
/// cliente HTTP entero del SDK para cinco líneas. Lo que la especificación fija es el orden, no la
/// biblioteca. El formato de firma se declara explícito (<see cref="DSASignatureFormat"/>) en vez
/// de dejarlo al valor por omisión: el proveedor firma en DER y el de .NET es IEEE P1363.
/// </para>
/// </summary>
public static class EventWebhookSignature
{
    /// <summary>
    /// Cuánto se admite que un lote tarde en llegar.
    /// <para>
    /// <b>Este número es nuestro.</b> El proveedor no publica ninguna tolerancia de antigüedad, así
    /// que no hay documentación suya que buscar: diez minutos es una decisión de esta plataforma,
    /// escrita acá para que quien lea después no salga a buscarla afuera.
    /// </para>
    /// </summary>
    public static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(10);

    /// <summary>El segundo Unix más chico que <see cref="DateTimeOffset"/> sabe representar.</summary>
    private const long MinUnixSeconds = -62135596800;

    /// <summary>El más grande.</summary>
    private const long MaxUnixSeconds = 253402300799;

    /// <summary>
    /// Lee la marca de tiempo de su cabecera.
    /// <para>
    /// El rango se comprueba acá y no en quien llama por un motivo concreto:
    /// <see cref="DateTimeOffset.FromUnixTimeSeconds"/> <b>lanza</b> fuera de
    /// [-62135596800, 253402300799], y <c>long.TryParse</c> acepta felizmente un
    /// <c>999999999999</c>. Construir la fecha sin comprobar el rango convierte una cabecera de
    /// cualquiera de internet en una excepción sin capturar —un 500— en el paso <b>anterior</b> a
    /// verificar la firma, que es justo donde este endpoint no puede permitirse una rama distinta.
    /// Fuera de rango es una marca de tiempo que no sirve, y se contesta como todas las demás.
    /// </para>
    /// </summary>
    public static bool TryReadSignedAt(string? timestamp, out DateTimeOffset signedAt)
    {
        signedAt = default;

        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return false;

        if (seconds < MinUnixSeconds || seconds > MaxUnixSeconds)
            return false;

        signedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);

        return true;
    }

    /// <summary>
    /// Si la marca de tiempo firmada cae dentro de la ventana.
    /// <para>
    /// Se rechaza por los dos lados. Por el pasado es lo que pide la especificación: un lote viejo
    /// reproducido no entra. Por el futuro es nuestro, y es la misma idea: una marca adelantada es
    /// la forma de esquivar el límite del pasado, y la holgura que hace falta para un reloj mal
    /// puesto ya cabe de sobra en diez minutos.
    /// </para>
    /// </summary>
    public static bool IsFresh(DateTimeOffset signedAt, DateTimeOffset now)
    {
        var drift = now - signedAt;

        return drift <= ReplayWindow && drift >= -ReplayWindow;
    }

    /// <summary>
    /// Si la firma corresponde a <paramref name="timestamp"/> concatenado con
    /// <paramref name="rawBody"/> bajo la clave pública configurada.
    /// <para>
    /// Devuelve <c>false</c> ante cualquier entrada mal formada —clave, firma o base64 inválidos—
    /// en vez de lanzar: para quien llama son el mismo hecho, "esto no está firmado por quien dice",
    /// y una excepción convertiría un dato del atacante en una rama distinta del código.
    /// </para>
    /// </summary>
    /// <param name="publicKeyBase64">La clave pública ECDSA del proveedor, en base64 (DER SPKI).</param>
    /// <param name="timestamp">La marca de tiempo tal como vino en su cabecera, sin normalizar.</param>
    /// <param name="rawBody">El cuerpo tal como llegó, byte por byte.</param>
    /// <param name="signatureBase64">La firma DER en base64, tal como vino en su cabecera.</param>
    public static bool IsValid(string? publicKeyBase64, string timestamp, string rawBody, string? signatureBase64)
    {
        ArgumentNullException.ThrowIfNull(timestamp);
        ArgumentNullException.ThrowIfNull(rawBody);

        if (string.IsNullOrWhiteSpace(publicKeyBase64) || string.IsNullOrWhiteSpace(signatureBase64))
            return false;

        if (!TryFromBase64(publicKeyBase64, out var publicKey) || !TryFromBase64(signatureBase64, out var signature))
            return false;

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);

            // La concatenación es la del proveedor: la marca de tiempo pegada al cuerpo, sin
            // separador y sin tocar ninguno de los dos.
            var signed = Encoding.UTF8.GetBytes(timestamp + rawBody);

            return ecdsa.VerifyData(
                signed,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException)
        {
            // Una clave que no es una clave, o una firma que no es DER. Es lo mismo que una firma
            // que no corresponde, y se responde igual.
            return false;
        }
    }

    private static bool TryFromBase64(string value, out byte[] bytes)
    {
        bytes = new byte[((value.Length * 3) + 3) / 4];

        if (Convert.TryFromBase64String(value, bytes, out var written))
        {
            bytes = bytes[..written];

            return true;
        }

        bytes = [];

        return false;
    }
}
