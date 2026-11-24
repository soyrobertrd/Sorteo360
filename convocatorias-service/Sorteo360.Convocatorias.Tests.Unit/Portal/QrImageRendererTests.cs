using Sorteo360.Convocatorias.Infrastructure.Portal;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Portal;

/// <summary>
/// La imagen del QR (spec <c>06</c> §4.5). Lo que se cobra es que salga un PNG de verdad y que la
/// generación no toque nada fuera de la memoria: se le da una URL y devuelve bytes, sin disco.
/// </summary>
public sealed class QrImageRendererTests
{
    private const string Payload = "https://app.pgti360.test/publico/entrada/abc123";

    private readonly QrImageRenderer _renderer = new();

    /// <summary>
    /// La firma del PNG son los mismos ocho bytes en todo archivo válido. Comprobarlos separa "un
    /// PNG" de "unos bytes cualesquiera".
    /// </summary>
    [Fact]
    public void RenderPng_ReturnsAValidPng_WhenGivenAPayload()
    {
        var png = _renderer.RenderPng(Payload);

        png.Length.ShouldBeGreaterThan(0);
        png[..8].ShouldBe([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    }

    /// <summary>El mismo texto da la misma imagen: la generación no lleva azar.</summary>
    [Fact]
    public void RenderPng_ReturnsTheSameBytes_WhenGivenTheSamePayload()
    {
        _renderer.RenderPng(Payload).ShouldBe(_renderer.RenderPng(Payload));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RenderPng_ThrowsArgumentException_WhenThePayloadIsBlank(string payload) =>
        Should.Throw<ArgumentException>(() => _renderer.RenderPng(payload));
}
