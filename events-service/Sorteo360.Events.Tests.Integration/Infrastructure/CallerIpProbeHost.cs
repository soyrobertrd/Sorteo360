using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Web;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>La API de prueba con <see cref="CallerIpProbeStartupFilter"/> delante de su tubería.</summary>
/// <remarks>
/// Se construye una sola vez por clase, al empezar. <see cref="EventsApiFactory"/> deja la
/// cadena de conexión en una variable de entorno y la borra al cerrarse, así que un host que se
/// construyera más tarde, test por test, podría encontrarla ya borrada por otra clase.
/// </remarks>
public sealed class CallerIpProbeHost : IDisposable
{
    private readonly EventsApiFactory _baseFactory = new();
    private readonly WebApplicationFactory<IApiMarker> _factory;

    public CallerIpProbeHost()
    {
        _factory = _baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter>(new CallerIpProbeStartupFilter())));
        Client = _factory.CreateClient();
    }

    /// <summary>Cliente contra la API con la sonda puesta.</summary>
    public HttpClient Client { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        _baseFactory.Dispose();
    }
}
