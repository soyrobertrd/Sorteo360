using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Platform.Auth;
using Polly;

namespace Sorteo360.Platform.Http;

/// <summary>
/// Registro de clientes tipados hacia otros servicios de la plataforma: un
/// <c>HttpClient</c> con su URL base, firmado con la identidad del servicio y envuelto en
/// una tubería de resiliencia.
/// <para>
/// <b>La frontera</b>: el chasis sabe autenticarse y sobrevivir a la red; no conoce ninguna
/// ruta, ningún DTO ni a qué servicio se llama. Lo único que recibe es el nombre con el que
/// ese destino está configurado (<c>Services:{nombre}:BaseUrl</c>). Todo lo demás —rutas,
/// contratos, traducción de respuestas— es del servicio que llama.
/// </para>
/// </summary>
public static class ServiceClientExtensions
{
    /// <summary>Tiempo máximo para hablar con el emisor de tokens.</summary>
    private static readonly TimeSpan TokenEndpointTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Registra <typeparamref name="TImplementation"/> como cliente tipado hacia el servicio
    /// <paramref name="serviceName"/>: URL base tomada de <c>Services:{serviceName}:BaseUrl</c>,
    /// cabecera <c>Authorization</c> con el token propio del servicio, y tubería de
    /// resiliencia (tiempo de espera, reintentos con jitter y cortacircuitos) compartida por
    /// todas las llamadas a ese destino.
    /// <para>
    /// Falla al arrancar si falta la URL base o la identidad del servicio: una llamada entre
    /// servicios mal configurada debe impedir el arranque, no descubrirse con la primera
    /// petición de un usuario.
    /// </para>
    /// </summary>
    /// <param name="configuration">Configuración del host, de donde salen <c>Services</c>, <c>ServiceAuth</c> y <c>Keycloak</c>.</param>
    /// <param name="serviceName">
    /// Nombre del destino tal como aparece en la configuración, ej. <c>Crm</c> para
    /// <c>Services__Crm__BaseUrl</c>. Da nombre además a su tubería de resiliencia, así que
    /// cada destino tiene su propio cortacircuitos.
    /// </param>
    public static IHttpClientBuilder AddServiceClient<TClient, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
        where TClient : class
        where TImplementation : class, TClient
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var baseUrl = ReadBaseUrl(configuration, serviceName);
        AddServiceIdentity(services, configuration);

        services.TryAddKeyedSingleton(serviceName, (serviceProvider, _) =>
            ServiceResiliencePipeline.Build(
                serviceName,
                serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ServiceResilienceHandler))));

        return services.AddHttpClient<TClient, TImplementation>(client => client.BaseAddress = baseUrl)
            // El orden importa: la resiliencia envuelve al token, así que cada reintento
            // sale firmado de nuevo en lugar de repetir un token que pudo caducar en vuelo.
            .AddHttpMessageHandler(serviceProvider => new ServiceResilienceHandler(
                serviceProvider.GetRequiredKeyedService<ResiliencePipeline<HttpResponseMessage>>(serviceName),
                serviceName))
            .AddHttpMessageHandler<ClientCredentialsTokenHandler>();
    }

    private static bool IsLocalUnconfiguredMode(IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration["Keycloak:Authority"])
        && string.IsNullOrWhiteSpace(configuration["ServiceAuth:ClientId"])
        && string.IsNullOrWhiteSpace(configuration["ServiceAuth:ClientSecret"]);

    /// <summary>URL base del destino; debe ser absoluta y apuntar al nombre de servicio de la red.</summary>
    private static Uri ReadBaseUrl(IConfiguration configuration, string serviceName)
    {
        var configurationKey = $"Services:{serviceName}:BaseUrl";
        var baseUrl = configuration[configurationKey];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (IsLocalUnconfiguredMode(configuration))
            {
                return new Uri($"http://localhost:5099/{serviceName.ToLowerInvariant()}/");
            }

            throw new InvalidOperationException(
                $"Falta la configuración del servicio '{serviceName}': se requiere '{configurationKey}'.");
        }

        // Sin esquema, "crm-service:8080" es una URI absoluta perfectamente válida cuyo
        // esquema sería "crm-service": hay que exigir http/https para atrapar la errata.
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"'{configurationKey}' debe ser una URL http o https absoluta "
                + $"(ej. http://{serviceName.ToLowerInvariant()}-service:8080).");
        }

        return uri;
    }

    /// <summary>
    /// Registra, una sola vez por proceso, todo lo que da identidad al servicio: sus
    /// credenciales, el cliente HTTP con el que habla con el emisor y el proveedor de token
    /// que las usa. Es común a todos los destinos, porque el servicio es uno solo llame a
    /// quien llame.
    /// </summary>
    private static void AddServiceIdentity(IServiceCollection services, IConfiguration configuration)
    {
        var serviceAuth = configuration.GetSection(ServiceAuthSettings.SectionName).Get<ServiceAuthSettings>()
            ?? new ServiceAuthSettings();

        var keycloak = configuration.GetSection(KeycloakSettings.SectionName).Get<KeycloakSettings>()
            ?? new KeycloakSettings();

        if (IsLocalUnconfiguredMode(configuration))
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IServiceTokenProvider, NoOpServiceTokenProvider>();
            services.TryAddTransient<ClientCredentialsTokenHandler>();
            return;
        }

        if (string.IsNullOrWhiteSpace(serviceAuth.ClientId) || string.IsNullOrWhiteSpace(serviceAuth.ClientSecret))
        {
            throw new InvalidOperationException(
                "Falta la identidad del servicio: se requieren 'ServiceAuth:ClientId' y 'ServiceAuth:ClientSecret'.");
        }

        if (string.IsNullOrWhiteSpace(keycloak.Authority))
        {
            throw new InvalidOperationException(
                "Falta el emisor de tokens: se requiere 'Keycloak:Authority' para pedir el token del servicio.");
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(serviceAuth);
        services.TryAddSingleton(keycloak);
        services.TryAddSingleton<IServiceTokenProvider, ClientCredentialsTokenProvider>();
        services.TryAddTransient<ClientCredentialsTokenHandler>();

        // Cliente propio para el emisor: el del cliente tipado lleva enganchado el handler
        // que pide el token, y pedirlo por ahí sería una recursión infinita.
        services.AddHttpClient(ClientCredentialsTokenProvider.HttpClientName,
            client => client.Timeout = TokenEndpointTimeout);
    }

    private sealed class NoOpServiceTokenProvider : IServiceTokenProvider
    {
        public Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken) =>
            Task.FromResult("local-dev-token");
    }
}
