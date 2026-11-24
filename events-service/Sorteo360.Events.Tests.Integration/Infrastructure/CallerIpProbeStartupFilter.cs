using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>Sonda para ver qué IP de quien llama le queda a la aplicación después de su tubería.</summary>
/// <remarks>
/// El <c>TestServer</c> no trae IP de origen, y sin ella <c>UseForwardedHeaders</c> no mira
/// <c>X-Forwarded-For</c>. Así que la sonda pone como IP de origen la que diga la cabecera
/// <see cref="PeerIpHeader"/> (quien entrega la petición: el proxy, o alguien de internet) y,
/// al empezar la respuesta, escribe en <see cref="CallerIpHeader"/> la IP que la aplicación
/// terminó viendo. Se registra como <c>IStartupFilter</c> para correr antes que la tubería
/// de la aplicación, y la respuesta empieza cuando esa tubería ya la reescribió.
/// </remarks>
public sealed class CallerIpProbeStartupFilter : IStartupFilter
{
    /// <summary>La IP desde la que el servicio ve llegar la conexión.</summary>
    public const string PeerIpHeader = "X-Test-Peer-Ip";

    /// <summary>La IP de quien llama, tal como la dejó la aplicación.</summary>
    public const string CallerIpHeader = "X-Test-Caller-Ip";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(PeerIpHeader, out var peerIp))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(peerIp.ToString());
                }

                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[CallerIpHeader] = context.Connection.RemoteIpAddress?.ToString();
                    return Task.CompletedTask;
                });

                await nextMiddleware(context);
            });

            next(app);
        };
    }
}
