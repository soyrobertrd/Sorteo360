using System.Security.Cryptography;
using System.Text;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// El par ECDSA con el que los tests firman lotes del webhook, generado <b>en memoria</b> en cada
/// corrida.
/// <para>
/// <b>Normativo: el par de desarrollo se genera, nunca se commitea.</b> Así los tests son
/// autocontenidos y CI no necesita ningún fixture de claves. Una clave de desarrollo commiteada no
/// se distingue de un vistazo de una de verdad, y la siguiente persona que la encuentre tiene que
/// demostrar que no vale nada antes de poder dejar de preocuparse.
/// </para>
/// </summary>
public sealed class WebhookSigningKey : IDisposable
{
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>La clave pública en base64, como se configura en <c>SendGrid__EventWebhook__PublicKey</c>.</summary>
    public string PublicKeyBase64 => Convert.ToBase64String(_ecdsa.ExportSubjectPublicKeyInfo());

    /// <summary>La firma DER en base64 de la marca de tiempo concatenada con el cuerpo crudo.</summary>
    public string Sign(string timestamp, string rawBody) =>
        Convert.ToBase64String(_ecdsa.SignData(
            Encoding.UTF8.GetBytes(timestamp + rawBody),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));

    public void Dispose() => _ecdsa.Dispose();
}
