using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.ProxyHeaders;

/// <summary>La IP real de quien llama detrás de Caddy y del nginx del frontend: dos proxies.</summary>
/// <remarks>
/// <c>X-Forwarded-For</c> vale solo cuando lo manda una red privada, y de él se toman dos saltos:
/// lo que alguien escriba más allá de dos saltos no cuenta. Se mira por <c>/_alive</c>
/// porque es anónima y la tubería es la misma para todas las rutas.
/// </remarks>
public sealed class ProxyHeadersTests : IClassFixture<CallerIpProbeHost>
{
    private const string NginxIp = "172.18.0.5";
    private const string CaddyIp = "172.18.0.4";
    private const string CallerIp = "203.0.113.7";

    private readonly HttpClient _client;

    public ProxyHeadersTests(CallerIpProbeHost host)
    {
        _client = host.Client;
    }

    /// <summary>Pide <c>/_alive</c> desde <paramref name="peerIp"/> y devuelve la IP que vio la aplicación.</summary>
    private async Task<string?> CallerIpSeenAsync(string peerIp, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_alive");
        request.Headers.Add(CallerIpProbeStartupFilter.PeerIpHeader, peerIp);
        request.Headers.Add("X-Forwarded-For", forwardedFor);

        using var response = await _client.SendAsync(request);

        return response.Headers.TryGetValues(CallerIpProbeStartupFilter.CallerIpHeader, out var values)
            ? values.Single()
            : null;
    }

    [Fact]
    public async Task GetAlive_SeesTheForwardedIp_WhenAPrivateNetworkProxySendsIt()
    {
        var seen = await CallerIpSeenAsync(peerIp: NginxIp, forwardedFor: CallerIp);

        seen.ShouldBe(CallerIp);
    }

    [Fact]
    public async Task GetAlive_IgnoresTheForwardedIp_WhenAPublicAddressSendsIt()
    {
        var seen = await CallerIpSeenAsync(peerIp: "198.51.100.20", forwardedFor: CallerIp);

        seen.ShouldBe("198.51.100.20");
    }

    [Fact]
    public async Task GetAlive_SeesTheClientBehindCaddyAndNginx_AndIgnoresAnythingFurtherBack()
    {
        // Prod: cliente -> Caddy -> nginx -> este servicio. Caddy escribe la IP del cliente y nginx
        // agrega la de Caddy. Con un solo salto la app vería a Caddy para todos (límite global,
        // auditoría run-1); con dos ve al cliente, y lo que el cliente inventó a la izquierda no cuenta.
        var seen = await CallerIpSeenAsync(peerIp: NginxIp, forwardedFor: $"198.51.100.99, {CallerIp}, {CaddyIp}");

        seen.ShouldBe(CallerIp);
    }
}
