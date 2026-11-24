using System.Security.Cryptography;
using Sorteo360.Notifications.Core.Webhooks;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Webhooks;

/// <summary>
/// La verificación de la firma, que es toda la autenticación que tiene el único endpoint anónimo
/// de terceros de la plataforma.
/// </summary>
public sealed class EventWebhookSignatureTests
{
    /// <summary>
    /// Un cuerpo con espaciado irregular a propósito: es lo que hace que una re-serialización se
    /// note. Cualquier normalización de espacios cambia los bytes y tira la firma.
    /// </summary>
    private const string RawBody = """[{"event":"delivered",  "email":"maria@sorteo360.local","timestamp":1757800000}]""";

    private const string Timestamp = "1757800000";

    [Fact]
    public void IsValid_ReturnsTrue_WhenSignedWithTheConfiguredKey()
    {
        using var key = new TestSigningKey();

        EventWebhookSignature.IsValid(key.PublicKeyBase64, Timestamp, RawBody, key.Sign(Timestamp, RawBody))
            .ShouldBeTrue();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenASingleSpaceIsAdded()
    {
        // Esto es lo que pasaría con model binding de por medio: el objeto sigue siendo el mismo y
        // los bytes ya no. Por eso el endpoint verifica el cuerpo crudo antes de parsear.
        using var key = new TestSigningKey();
        var signature = key.Sign(Timestamp, RawBody);

        var reserialized = RawBody.Replace(",  ", ", ", StringComparison.Ordinal);

        reserialized.ShouldNotBe(RawBody);
        EventWebhookSignature.IsValid(key.PublicKeyBase64, Timestamp, reserialized, signature).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenTheTimestampChanges()
    {
        using var key = new TestSigningKey();

        EventWebhookSignature.IsValid(key.PublicKeyBase64, "1757800001", RawBody, key.Sign(Timestamp, RawBody))
            .ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenSignedWithAnotherKey()
    {
        using var ours = new TestSigningKey();
        using var theirs = new TestSigningKey();

        EventWebhookSignature.IsValid(ours.PublicKeyBase64, Timestamp, RawBody, theirs.Sign(Timestamp, RawBody))
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_ReturnsFalse_WhenNoPublicKeyIsConfigured(string? publicKey)
    {
        // El fallo que de verdad va a ocurrir es un despliegue a medias. Aceptar el lote entonces
        // abriría el endpoint a cualquiera.
        using var key = new TestSigningKey();

        EventWebhookSignature.IsValid(publicKey, Timestamp, RawBody, key.Sign(Timestamp, RawBody))
            .ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenThePublicKeyIsNotBase64()
    {
        using var key = new TestSigningKey();

        EventWebhookSignature.IsValid("no-es-base64!!", Timestamp, RawBody, key.Sign(Timestamp, RawBody))
            .ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenThePublicKeyIsBase64ButNotAKey()
    {
        using var key = new TestSigningKey();

        EventWebhookSignature.IsValid(
                Convert.ToBase64String([1, 2, 3, 4]), Timestamp, RawBody, key.Sign(Timestamp, RawBody))
            .ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenTheSignatureIsNotDer()
    {
        using var key = new TestSigningKey();

        EventWebhookSignature.IsValid(key.PublicKeyBase64, Timestamp, RawBody, Convert.ToBase64String([9, 9, 9]))
            .ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenTheSignatureIsIeeeP1363()
    {
        // El proveedor firma en DER y el valor por omisión de .NET es P1363. Declarar el formato
        // no es decoración: sin esta línea nada verificaría nunca, y el síntoma sería un 403 en
        // producción y ninguna pista.
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p1363 = ecdsa.SignData(
            System.Text.Encoding.UTF8.GetBytes(Timestamp + RawBody), HashAlgorithmName.SHA256);

        EventWebhookSignature.IsValid(
                Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
                Timestamp,
                RawBody,
                Convert.ToBase64String(p1363))
            .ShouldBeFalse();
    }

    [Fact]
    public void ReplayWindow_ReturnsTenMinutes_WhenRead() =>
        // El proveedor no publica ninguna tolerancia de antigüedad: este número es de esta
        // plataforma, y el test existe para que cambiarlo sea una decisión y no un descuido.
        EventWebhookSignature.ReplayWindow.ShouldBe(TimeSpan.FromMinutes(10));

    [Fact]
    public void IsFresh_ReturnsTrue_WhenSignedNineMinutesAgo()
    {
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

        EventWebhookSignature.IsFresh(now.AddMinutes(-9), now).ShouldBeTrue();
    }

    [Fact]
    public void IsFresh_ReturnsFalse_WhenSignedElevenMinutesAgo()
    {
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

        EventWebhookSignature.IsFresh(now.AddMinutes(-11), now).ShouldBeFalse();
    }

    [Fact]
    public void TryReadSignedAt_ReturnsTheInstant_WhenTheTimestampIsNormal()
    {
        EventWebhookSignature.TryReadSignedAt("1757800000", out var signedAt).ShouldBeTrue();

        signedAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1757800000));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ayer")]
    [InlineData("17578.00000")]
    [InlineData("1,757,800,000")]
    public void TryReadSignedAt_ReturnsFalse_WhenTheTimestampIsNotWholeSeconds(string? timestamp) =>
        EventWebhookSignature.TryReadSignedAt(timestamp, out _).ShouldBeFalse();

    [Theory]
    [InlineData("999999999999")]
    [InlineData("-999999999999")]
    [InlineData("9223372036854775807")]
    public void TryReadSignedAt_ReturnsFalse_WhenTheNumberIsNotARepresentableInstant(string timestamp) =>
        // Sin esta comprobación, DateTimeOffset.FromUnixTimeSeconds lanza y la excepción sale sin
        // capturar ANTES de verificar la firma: una cabecera de cualquiera de internet se volvería
        // un 500 en el único endpoint anónimo de terceros de la plataforma.
        EventWebhookSignature.TryReadSignedAt(timestamp, out _).ShouldBeFalse();

    [Fact]
    public void TryReadSignedAt_ReturnsTrueOnlyInsideTheRange_WhenTheNumberIsAtTheRangeEdge()
    {
        EventWebhookSignature.TryReadSignedAt("-62135596800", out _).ShouldBeTrue();
        EventWebhookSignature.TryReadSignedAt("253402300799", out _).ShouldBeTrue();
        EventWebhookSignature.TryReadSignedAt("-62135596801", out _).ShouldBeFalse();
        EventWebhookSignature.TryReadSignedAt("253402300800", out _).ShouldBeFalse();
    }

    [Fact]
    public void IsFresh_ReturnsFalse_WhenTheTimestampIsFarInTheFuture()
    {
        // Si solo se mirara el pasado, adelantar la marca sería la forma de esquivar la ventana.
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

        EventWebhookSignature.IsFresh(now.AddMinutes(11), now).ShouldBeFalse();
    }
}
