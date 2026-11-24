using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// Le pone IP de origen a las peticiones del <c>TestServer</c>, que no trae ninguna.
/// <para>
/// Es lo mínimo para poder ejercitar <c>UseForwardedHeaders</c> de verdad: el middleware
/// solo recorre <c>X-Forwarded-For</c> mientras quien entrega la petición esté en una red
/// de confianza, y sin IP de origen no entra en el primer salto. Se registra como
/// <c>IStartupFilter</c> para correr antes que la tubería de la aplicación.
/// </para>
/// </summary>
public sealed class RemoteIpStartupFilter : IStartupFilter
{
    private readonly IPAddress _remoteIpAddress;

    public RemoteIpStartupFilter(string remoteIpAddress)
    {
        ArgumentNullException.ThrowIfNull(remoteIpAddress);
        _remoteIpAddress = IPAddress.Parse(remoteIpAddress);
    }

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = _remoteIpAddress;
                await nextMiddleware(context);
            });

            next(app);
        };
    }
}
