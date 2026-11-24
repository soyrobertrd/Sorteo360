using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

// IPNetwork existe en los dos espacios de nombres de arriba; el que toma KnownIPNetworks
// es el de System.Net.
using IPNetwork = System.Net.IPNetwork;

namespace Sorteo360.Platform.Http;

/// <summary>Cómo se lee la IP real de quien llama cuando la petición llegó por proxy.</summary>
/// <remarks>
/// <para>
/// Sin esto, todo lo que mira la IP, como un limitador por IP, ve la del contenedor del
/// último proxy: Traefik termina TLS y reenvía, y cada petición parece venir de la misma
/// dirección. Un límite de sesenta por minuto pasaría a ser sesenta por minuto para todo
/// internet junto.
/// </para>
/// <para>
/// Lo que cambia entre servicios es cuántos proxies hay delante, y lo pasa cada uno; de quién
/// se acepta la cabecera es igual para todos y se decide aquí. El servicio llama además a
/// <c>app.UseForwardedHeaders()</c> al principio de su tubería, antes de todo lo que lea la IP.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public static class ProxyHeadersConfiguration
{
    /// <summary>Quién puede hablar por otro: solo las redes privadas.</summary>
    /// <remarks>
    /// Los proxies viven en la red de Compose, y una cabecera que llegue de una IP pública se
    /// descarta sola.
    /// </remarks>
    private static readonly (string Prefix, int Length)[] TrustedNetworks =
    [
        ("10.0.0.0", 8),
        ("172.16.0.0", 12),
        ("192.168.0.0", 16),
        ("127.0.0.0", 8),
        ("::1", 128),
    ];

    /// <summary>
    /// Registra las opciones que consume <c>UseForwardedHeaders</c>. La lista de fábrica
    /// (solo loopback) se limpia porque los proxies son contenedores vecinos, no localhost.
    /// </summary>
    /// <param name="services">Contenedor de servicios.</param>
    /// <param name="proxyHopCount">
    /// Saltos de proxy que se recorren hacia atrás en <c>X-Forwarded-For</c>: uno por proxy
    /// delante del servicio. Un límite y no "todos" para que nadie se invente saltos
    /// escribiendo la cabecera a mano.
    /// </param>
    public static IServiceCollection AddProxyHeaders(this IServiceCollection services, int proxyHopCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(proxyHopCount);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = proxyHopCount;

            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var (prefix, length) in TrustedNetworks)
            {
                options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse(prefix), length));
            }
        });

        return services;
    }
}
