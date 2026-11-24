using Sorteo360.Convocatorias.Core.Portal;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Portal;

/// <summary>
/// Los dos enlaces de la invitación. Lo que se cobra es <b>dónde va el token</b>: en el
/// fragmento, que el navegador no manda al servidor, y no en la cadena de consulta, que acaba en
/// el registro de acceso de nginx, de Traefik y de la red del cliente (spec <c>06</c> §2.1).
/// </summary>
public sealed class PortalLinkTests
{
    private const string BaseUrl = "https://app.pgti360.test";

    [Fact]
    public void Registration_ReturnsTheTokenInTheFragment_WhenItBuilds()
    {
        PortalLink.Registration(BaseUrl, "abc123")
            .ShouldBe("https://app.pgti360.test/publico/registro#t=abc123");
    }

    /// <summary>
    /// El de baja es otra ruta y otra clave de fragmento. Va separado del de registro porque el
    /// de registro está pensado para reenviarse (spec <c>06</c> §2.4).
    /// </summary>
    [Fact]
    public void Unsubscribe_ReturnsADifferentPathAndKey_WhenItBuilds()
    {
        PortalLink.Unsubscribe(BaseUrl, "abc123")
            .ShouldBe("https://app.pgti360.test/publico/baja#u=abc123");
    }

    /// <summary>
    /// La base la escribe una persona en un compose y las dos formas son igual de razonables de
    /// escribir. Sin esto la mitad de los despliegues mandarían enlaces con doble barra.
    /// </summary>
    [Fact]
    public void Registration_ReturnsTheSameLink_WhenTheBaseHasATrailingSlash()
    {
        PortalLink.Registration($"{BaseUrl}/", "abc123")
            .ShouldBe(PortalLink.Registration(BaseUrl, "abc123"));
    }

    [Theory]
    [InlineData("", "abc123")]
    [InlineData("  ", "abc123")]
    [InlineData(BaseUrl, "")]
    public void Registration_Throws_WhenTheBaseOrTheTokenIsBlank(string baseUrl, string token) =>
        Should.Throw<ArgumentException>(() => PortalLink.Registration(baseUrl, token));

    /// <summary>
    /// La página de la entrada lleva el <c>qr_code</c> <b>en la ruta, no en el fragmento</b>: es
    /// lo que codifica el QR y para que un escáner lo lea el servidor tiene que verlo (spec
    /// <c>06</c> §2.3).
    /// </summary>
    [Fact]
    public void Ticket_ReturnsTheCodeInThePath_WhenItBuilds()
    {
        PortalLink.Ticket(BaseUrl, "abc123")
            .ShouldBe("https://app.pgti360.test/publico/entrada/abc123");
    }

    [Fact]
    public void Ticket_ReturnsTheSameLink_WhenTheBaseHasATrailingSlash()
    {
        PortalLink.Ticket($"{BaseUrl}/", "abc123")
            .ShouldBe(PortalLink.Ticket(BaseUrl, "abc123"));
    }

    [Theory]
    [InlineData("", "abc123")]
    [InlineData("  ", "abc123")]
    [InlineData(BaseUrl, "")]
    public void Ticket_ThrowsArgumentException_WhenTheBaseOrTheCodeIsBlank(string baseUrl, string qrCode) =>
        Should.Throw<ArgumentException>(() => PortalLink.Ticket(baseUrl, qrCode));

    /// <summary>
    /// La URL de la imagen del QR es la del endpoint público que la genera al vuelo, no una del
    /// portal: la sirve este servicio bajo <c>/v1/public</c>, alcanzable desde el mismo origen que
    /// el portal a través de nginx. Es lo que va en <c>{{qr}}</c> del correo de confirmación
    /// (spec <c>06</c> §4.4 y §4.5).
    /// </summary>
    [Fact]
    public void QrImage_ReturnsThePublicEndpointUrl_WhenItBuilds()
    {
        PortalLink.QrImage(BaseUrl, "abc123")
            .ShouldBe("https://app.pgti360.test/v1/public/registrations/abc123/qr");
    }

    [Fact]
    public void QrImage_ReturnsTheSameUrl_WhenTheBaseHasATrailingSlash()
    {
        PortalLink.QrImage($"{BaseUrl}/", "abc123")
            .ShouldBe(PortalLink.QrImage(BaseUrl, "abc123"));
    }

    [Theory]
    [InlineData("", "abc123")]
    [InlineData("  ", "abc123")]
    [InlineData(BaseUrl, "")]
    public void QrImage_Throws_WhenTheBaseOrTheCodeIsBlank(string baseUrl, string qrCode) =>
        Should.Throw<ArgumentException>(() => PortalLink.QrImage(baseUrl, qrCode));
}
