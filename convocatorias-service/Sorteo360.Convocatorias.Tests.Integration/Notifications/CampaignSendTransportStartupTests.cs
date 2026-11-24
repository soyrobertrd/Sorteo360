using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Notifications;

/// <summary>
/// Qué hace el servicio cuando no le dicen por dónde mandar el correo (spec <c>04</c> §4).
/// <para>
/// <b>Se cae al arrancar, y eso es la funcionalidad.</b> Por qué, está en
/// <see cref="CampaignSendTransport"/>. Lo que agrega este test es que se cae <b>al arrancar</b>:
/// un contenedor en bucle de reinicio se ve, y una campaña que informa que salió sin escribirle a
/// nadie no.
/// </para>
/// <para>
/// Sin base de datos: lo que se cobra pasa antes de que el host acepte una petición.
/// </para>
/// </summary>
public sealed class CampaignSendTransportStartupTests
{
    [Fact]
    public void Startup_Throws_WhenTheTransportIsNotConfigured()
    {
        using var factory = new TransportFactory(configured: null);

        var thrown = Should.Throw<InvalidOperationException>(() => factory.CreateClient());

        thrown.Message.ShouldContain(CampaignSendTransport.SettingKey);
    }

    [Fact]
    public void Startup_Throws_WhenTheTransportIsNotATransport()
    {
        using var factory = new TransportFactory("smtp");

        Should.Throw<InvalidOperationException>(() => factory.CreateClient())
            .Message.ShouldContain("smtp");
    }

    /// <summary>
    /// El otro olvido de la misma familia, y con el mismo desenlace: el transporte dice
    /// <c>http</c> y nadie dijo dónde está notification-service. Lo cobra <c>AddServiceClient</c>
    /// del chasis, pero se prueba acá porque lo que importa no es quién lanza sino <b>cuándo</b>:
    /// arrancar igual y no escribirle a nadie es el fail-open que la spec <c>04</c> §4 descarta
    /// por su nombre.
    /// </summary>
    [Fact]
    public void Startup_Throws_WhenTheHttpTransportHasNoBaseUrl()
    {
        using var factory = new TransportFactory(CampaignSendTransport.Http, notificationsBaseUrl: string.Empty);

        Should.Throw<InvalidOperationException>(() => factory.CreateClient())
            .Message.ShouldContain("Services:Notifications:BaseUrl");
    }

    /// <summary>
    /// El tercero de la familia: el transporte dice <c>http</c> y nadie dijo quién firma. El
    /// emisor contestaría <c>400</c> a cada lote y a cada confirmación, así que se cae al
    /// arrancar en vez de aceptar campañas que no salen.
    /// </summary>
    [Fact]
    public void CreateClient_ReturnsAnOptionsValidationFailure_WhenTheHttpTransportHasNoFromAddress()
    {
        using var factory = new TransportFactory(CampaignSendTransport.Http, fromAddress: string.Empty);

        Should.Throw<OptionsValidationException>(() => factory.CreateClient())
            .Message.ShouldContain("FromAddress");
    }

    /// <summary>
    /// Con <c>stub</c> no sale nada, así que nadie firma: que falte el remitente no lo tumba.
    /// </summary>
    [Fact]
    public void CreateClient_ReturnsAClient_WhenTheStubTransportHasNoFromAddress()
    {
        using var factory = new TransportFactory(CampaignSendTransport.Stub, fromAddress: string.Empty);

        Should.NotThrow(() => factory.CreateClient());
    }

    /// <summary>
    /// El mismo host que el resto de la suite, con el transporte puesto a mano después de que
    /// <c>UseStubbedServiceIdentity</c> ponga el suyo.
    /// </summary>
    private sealed class TransportFactory : WebApplicationFactory<IApiMarker>
    {
        private readonly string? _configured;
        private readonly string _notificationsBaseUrl;
        private readonly string _fromAddress;

        public TransportFactory(
            string? configured,
            string notificationsBaseUrl = "http://notifications.invalid",
            string fromAddress = ServiceIdentityStub.FromAddress)
        {
            _configured = configured;
            _notificationsBaseUrl = notificationsBaseUrl;
            _fromAddress = fromAddress;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTesting");
            builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=test;Username=test;Password=test");
            builder.UseSetting("Services:Crm:BaseUrl", "http://crm.invalid");
            builder.UseSetting("Services:Events:BaseUrl", "http://events.invalid");
            builder.UseSetting("Services:Notifications:BaseUrl", _notificationsBaseUrl);
            builder.UseSetting("ServiceAuth:ClientId", "convocatorias-service");
            builder.UseSetting("ServiceAuth:ClientSecret", "integration-testing");
            builder.UseSetting("Keycloak:Authority", "http://keycloak.invalid/realms/pgti360");
            builder.UseSetting("Portal:BaseUrl", ServiceIdentityStub.PortalBaseUrl);
            builder.UseSetting("Notifications:FromAddress", _fromAddress);

            // Vacío y no ausente: `UseSetting` no sabe quitar una clave, y el ajuste trata las dos
            // cosas igual a propósito, porque un panel que deja el valor en blanco es el olvido
            // más probable de los dos.
            builder.UseSetting(CampaignSendTransport.SettingKey, _configured ?? string.Empty);
        }
    }
}
