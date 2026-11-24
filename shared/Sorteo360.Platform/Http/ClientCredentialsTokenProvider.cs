using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sorteo360.Platform.Auth;

namespace Sorteo360.Platform.Http;

/// <summary>
/// Pide el token del servicio al realm por <c>client_credentials</c> y lo guarda en memoria
/// hasta poco antes de que caduque. Es un singleton: la caché es del proceso, así que todas
/// las llamadas salientes del servicio —a cuantos destinos sea— comparten un token.
/// <para>
/// Usa un cliente HTTP con nombre propio (<see cref="HttpClientName"/>) y no el del cliente
/// tipado: el del cliente tipado lleva enganchado el <see cref="ClientCredentialsTokenHandler"/>,
/// y pedir el token por ahí sería una recursión infinita.
/// </para>
/// </summary>
public sealed class ClientCredentialsTokenProvider : IServiceTokenProvider, IDisposable
{
    /// <summary>Nombre del cliente HTTP con el que se habla con el emisor de tokens.</summary>
    public const string HttpClientName = "platform-service-auth";

    /// <summary>
    /// Margen con el que se renueva el token antes de su caducidad, para que ninguna
    /// petición salga con un token que caduque en vuelo.
    /// </summary>
    private static readonly TimeSpan ExpiryLeeway = TimeSpan.FromSeconds(30);

    /// <summary>Vigencia asumida si el emisor no informa <c>expires_in</c>.</summary>
    private static readonly TimeSpan FallbackLifetime = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServiceAuthSettings _serviceAuth;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ClientCredentialsTokenProvider> _logger;
    private readonly string _tokenEndpoint;
    private readonly SemaphoreSlim _sync = new(1, 1);

    /// <summary>
    /// Token vigente y el momento en que toca renovarlo, en una sola referencia inmutable:
    /// el camino rápido lee sin tomar el semáforo, y leer dos campos sueltos podría
    /// emparejar un token con la caducidad del anterior.
    /// </summary>
    private CachedToken? _cached;

    public ClientCredentialsTokenProvider(
        IHttpClientFactory httpClientFactory,
        KeycloakSettings keycloakSettings,
        ServiceAuthSettings serviceAuthSettings,
        TimeProvider timeProvider,
        ILogger<ClientCredentialsTokenProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(keycloakSettings);
        ArgumentNullException.ThrowIfNull(serviceAuthSettings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _serviceAuth = serviceAuthSettings;
        _timeProvider = timeProvider;
        _logger = logger;
        // El endpoint de token es el del realm que ya emite y valida los JWT entrantes:
        // una sola variable de entorno describe la identidad de todo el servicio.
        _tokenEndpoint = $"{keycloakSettings.Authority.TrimEnd('/')}/protocol/openid-connect/token";
    }

    /// <inheritdoc />
    public async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        // Referencia vista antes de esperar el turno: si otro llamante renueva mientras
        // tanto, su token sirve y una ráfaga de 401 no dispara una ráfaga de renovaciones.
        var seenBeforeWait = Volatile.Read(ref _cached);

        if (!forceRefresh && IsUsable(seenBeforeWait))
        {
            return seenBeforeWait.AccessToken;
        }

        await _sync.WaitAsync(cancellationToken);
        try
        {
            var current = Volatile.Read(ref _cached);
            if (IsUsable(current) && (!forceRefresh || !ReferenceEquals(current, seenBeforeWait)))
            {
                return current.AccessToken;
            }

            var token = await RequestTokenAsync(cancellationToken);
            var renewed = new CachedToken(token.AccessToken, CalculateRenewAt(token.ExpiresIn));
            Volatile.Write(ref _cached, renewed);
            return renewed.AccessToken;
        }
        finally
        {
            _sync.Release();
        }
    }

    public void Dispose() => _sync.Dispose();

    /// <summary>Indica si el token guardado existe y aún le queda margen de vida.</summary>
    private bool IsUsable([NotNullWhen(true)] CachedToken? cached) =>
        cached is not null && _timeProvider.GetUtcNow() < cached.RenewAt;

    /// <summary>Momento a partir del cual toca renovar: la caducidad menos el margen.</summary>
    private DateTimeOffset CalculateRenewAt(int expiresInSeconds)
    {
        var lifetime = expiresInSeconds > 0
            ? TimeSpan.FromSeconds(expiresInSeconds)
            : FallbackLifetime;

        if (expiresInSeconds <= 0)
        {
            _logger.LogWarning(
                "El emisor no informó expires_in para el cliente {ClientId}; se asume una vigencia de {Lifetime}.",
                _serviceAuth.ClientId,
                FallbackLifetime);
        }

        var renewIn = lifetime > ExpiryLeeway ? lifetime - ExpiryLeeway : TimeSpan.Zero;
        return _timeProvider.GetUtcNow() + renewIn;
    }

    /// <summary>
    /// Pide un token nuevo al realm. Todo fallo —de red, de credenciales o de formato—
    /// sale como <see cref="ServiceAuthenticationException"/>, y su mensaje nunca lleva
    /// el secreto del cliente.
    /// </summary>
    private async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _serviceAuth.ClientId,
                ["client_secret"] = _serviceAuth.ClientSecret,
            }),
        };

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogError(ex, "No se pudo contactar con el emisor de tokens en {TokenEndpoint}.", _tokenEndpoint);
            throw TokenFailure("el emisor no responde.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "El emisor de tokens rechazó las credenciales del cliente {ClientId} con el estado {StatusCode}.",
                    _serviceAuth.ClientId,
                    (int)response.StatusCode);
                throw TokenFailure($"el emisor respondió {(int)response.StatusCode}.");
            }

            TokenResponse? token;
            try
            {
                token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            }
            catch (JsonException ex)
            {
                throw TokenFailure("la respuesta del emisor no es JSON válido.", ex);
            }

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw TokenFailure("la respuesta del emisor no trae access_token.");
            }

            return token;
        }
    }

    /// <summary>
    /// Fallo al obtener el token, con el cliente que lo pedía y la causa. Nombra el
    /// <c>ClientId</c> porque es lo que hay que revisar en el realm; el secreto nunca
    /// entra en el mensaje.
    /// </summary>
    private ServiceAuthenticationException TokenFailure(string cause, Exception? innerException = null) =>
        new($"No se pudo obtener el token del cliente '{_serviceAuth.ClientId}': {cause}", innerException);

    /// <summary>Token guardado y el momento a partir del cual toca renovarlo.</summary>
    private sealed record CachedToken(string AccessToken, DateTimeOffset RenewAt);

    /// <summary>Respuesta del endpoint de token del realm.</summary>
    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
