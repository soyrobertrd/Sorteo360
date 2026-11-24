using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Sorteo360.Platform.Http;

/// <summary>
/// Firma cada petición saliente con el token del servicio y, si el destino responde 401,
/// la repite una única vez con un token recién pedido.
/// <para>
/// El reintento existe porque un token puede dejar de valer antes de caducar —rotación de
/// claves del realm, cliente reconfigurado— y en ese caso la caché guarda algo que el
/// destino ya rechaza. Se reintenta una sola vez: si el segundo token también recibe 401,
/// el problema es de configuración (audiencia o rol) y repetir no lo arregla.
/// </para>
/// <para>
/// Un 403 no se reintenta: ahí el token es válido y el destino está negando la operación.
/// </para>
/// </summary>
public sealed class ClientCredentialsTokenHandler : DelegatingHandler
{
    private readonly IServiceTokenProvider _tokenProvider;
    private readonly ILogger<ClientCredentialsTokenHandler> _logger;

    public ClientCredentialsTokenHandler(
        IServiceTokenProvider tokenProvider,
        ILogger<ClientCredentialsTokenHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await _tokenProvider.GetTokenAsync(forceRefresh: false, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        _logger.LogWarning(
            "{Destination} rechazó el token del servicio (401); se reintenta una vez con uno nuevo.",
            request.RequestUri?.GetLeftPart(UriPartial.Authority));

        response.Dispose();

        // Reenviar el mismo mensaje es válido mientras el contenido se pueda releer, que es
        // el caso de los cuerpos en memoria (StringContent, JsonContent) que usan los
        // clientes tipados de la plataforma.
        var refreshedToken = await _tokenProvider.GetTokenAsync(forceRefresh: true, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
