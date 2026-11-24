using System.Security.Cryptography;
using System.Text;

namespace Sorteo360.Notifications.Tests.Unit.Webhooks;

/// <summary>
/// Un par de claves ECDSA generado <b>en memoria</b> para firmar lotes de prueba.
/// <para>
/// <b>Normativo: el par de desarrollo se genera, nunca se commitea.</b> Los tests generan el suyo
/// en cada corrida, así que son autocontenidos, deterministas y CI no necesita ningún fixture de
/// claves. Una clave de desarrollo commiteada no se distingue de un vistazo de una de verdad, y la
/// siguiente persona que la encuentre tiene que demostrar que no vale nada antes de poder dejar de
/// preocuparse.
/// </para>
/// </summary>
internal sealed class TestSigningKey : IDisposable
{
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>La clave pública en base64, tal como se configura en <c>SendGrid__EventWebhook__PublicKey</c>.</summary>
    public string PublicKeyBase64 => Convert.ToBase64String(_ecdsa.ExportSubjectPublicKeyInfo());

    /// <summary>
    /// La firma de <paramref name="timestamp"/> concatenado con <paramref name="rawBody"/>, en
    /// base64 y en DER, que es como firma el proveedor.
    /// </summary>
    public string Sign(string timestamp, string rawBody)
    {
        var signature = _ecdsa.SignData(
            Encoding.UTF8.GetBytes(timestamp + rawBody),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        return Convert.ToBase64String(signature);
    }

    public void Dispose() => _ecdsa.Dispose();
}
