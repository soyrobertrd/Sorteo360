using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Platform.Http;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Http;

/// <summary>
/// Registro de un cliente tipado hacia otro servicio: de dónde sale su URL base, qué
/// configuración exige al arrancar, y qué le engancha por debajo (identidad del servicio
/// y resiliencia). El chasis no conoce ninguna ruta ni DTO del destino: solo el nombre con
/// el que se configura.
/// </summary>
public sealed class ServiceClientExtensionsTests
{
    private const string ServiceName = "Crm";
    private const string BaseUrl = "http://crm-service:8080";

    private readonly IServiceTokenProvider _tokenProvider = Substitute.For<IServiceTokenProvider>();

    public ServiceClientExtensionsTests()
    {
        _tokenProvider.GetTokenAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("token-de-prueba"));
    }

    /// <summary>Cliente tipado de prueba: hace las veces del cliente CRM de events-service.</summary>
    private interface ITestServiceClient
    {
        Task<HttpResponseMessage> GetAsync(CancellationToken cancellationToken);
    }

    private sealed class TestServiceClient : ITestServiceClient
    {
        private readonly HttpClient _httpClient;

        public TestServiceClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Uri? BaseAddress => _httpClient.BaseAddress;

        public Task<HttpResponseMessage> GetAsync(CancellationToken cancellationToken) =>
            _httpClient.GetAsync("/v1/opts", cancellationToken);
    }

    private static IConfiguration CreateConfiguration(
        string? baseUrl = BaseUrl,
        string? clientId = "events-service",
        string? clientSecret = "un-secreto",
        string? authority = "http://keycloak:8080/realms/pgti360",
        string? secondServiceBaseUrl = null)
    {
        var values = new Dictionary<string, string?>
        {
            [$"Services:{ServiceName}:BaseUrl"] = baseUrl,
            ["Services:Convocatorias:BaseUrl"] = secondServiceBaseUrl,
            ["ServiceAuth:ClientId"] = clientId,
            ["ServiceAuth:ClientSecret"] = clientSecret,
            ["Keycloak:Authority"] = authority,
            ["Keycloak:Audience"] = "events-api",
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>
    /// Colección con lo que el host ya trae (logging) y con el proveedor de token
    /// sustituido, para no hablar con un Keycloak real.
    /// </summary>
    private ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_tokenProvider);
        return services;
    }

    /// <summary>
    /// Registra el cliente contra un destino falso y lo resuelve, que es el arreglo común
    /// de todas las pruebas de la cadena de handlers.
    /// </summary>
    private (ITestServiceClient Client, ServiceProvider Provider) CreateClientAgainst(
        FakeHttpMessageHandler destination)
    {
        var services = CreateServices();
        services.AddServiceClient<ITestServiceClient, TestServiceClient>(CreateConfiguration(), ServiceName)
            .ConfigurePrimaryHttpMessageHandler(() => destination);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<ITestServiceClient>(), provider);
    }

    #region URL base

    [Fact]
    public void AddServiceClient_SetsTheBaseAddress_WhenTheServicesSectionCarriesTheUrl()
    {
        var services = CreateServices();

        services.AddServiceClient<ITestServiceClient, TestServiceClient>(CreateConfiguration(), ServiceName);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ITestServiceClient>().ShouldBeOfType<TestServiceClient>();
        client.BaseAddress.ShouldBe(new Uri(BaseUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddServiceClient_Throws_WhenTheBaseUrlIsMissing(string? baseUrl)
    {
        var services = CreateServices();

        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddServiceClient<ITestServiceClient, TestServiceClient>(
                CreateConfiguration(baseUrl: baseUrl), ServiceName));

        exception.Message.ShouldContain($"Services:{ServiceName}:BaseUrl");
    }

    [Theory]
    [InlineData("crm-service:8080")]  // sin esquema: URI absoluta válida, pero no una URL
    [InlineData("/v1/opts")]
    public void AddServiceClient_Throws_WhenTheBaseUrlIsNotAnHttpUrl(string baseUrl)
    {
        var services = CreateServices();

        Should.Throw<InvalidOperationException>(() =>
            services.AddServiceClient<ITestServiceClient, TestServiceClient>(
                CreateConfiguration(baseUrl: baseUrl), ServiceName));
    }

    #endregion

    #region Identidad exigida al arrancar

    [Theory]
    [InlineData(null, "un-secreto")]
    [InlineData("events-service", null)]
    public void AddServiceClient_Throws_WhenTheServiceIdentityIsIncomplete(string? clientId, string? clientSecret)
    {
        var services = CreateServices();

        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddServiceClient<ITestServiceClient, TestServiceClient>(
                CreateConfiguration(clientId: clientId, clientSecret: clientSecret), ServiceName));

        exception.Message.ShouldContain("ServiceAuth");
    }

    [Fact]
    public void AddServiceClient_Throws_WhenTheKeycloakAuthorityIsMissing()
    {
        var services = CreateServices();

        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddServiceClient<ITestServiceClient, TestServiceClient>(
                CreateConfiguration(authority: null), ServiceName));

        exception.Message.ShouldContain("Keycloak:Authority");
    }

    #endregion

    #region Una identidad, varios destinos

    [Fact]
    public void AddServiceClient_SharesOneTokenProvider_WhenSeveralDestinationsAreRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = CreateConfiguration(secondServiceBaseUrl: "http://convocatorias-service:8080");

        services.AddServiceClient<ITestServiceClient, TestServiceClient>(configuration, ServiceName);
        services.AddServiceClient<ISecondTestServiceClient, SecondTestServiceClient>(configuration, "Convocatorias");

        using var provider = services.BuildServiceProvider();
        // La identidad del servicio es una sola: un token en caché sirve para todo destino.
        provider.GetRequiredService<IServiceTokenProvider>()
            .ShouldBeSameAs(provider.GetRequiredService<IServiceTokenProvider>());
        provider.GetRequiredService<ITestServiceClient>().ShouldBeOfType<TestServiceClient>()
            .BaseAddress.ShouldBe(new Uri(BaseUrl));
        provider.GetRequiredService<ISecondTestServiceClient>().ShouldBeOfType<SecondTestServiceClient>()
            .BaseAddress.ShouldBe(new Uri("http://convocatorias-service:8080"));
    }

    /// <summary>Segundo destino: DI exige un tipo distinto por cliente tipado.</summary>
    private interface ISecondTestServiceClient;

    private sealed class SecondTestServiceClient : ISecondTestServiceClient
    {
        private readonly HttpClient _httpClient;

        public SecondTestServiceClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Uri? BaseAddress => _httpClient.BaseAddress;
    }

    #endregion

    #region Cadena de handlers

    [Fact]
    public async Task AddServiceClient_SignsTheCallWithTheServiceToken_WhenTheClientIsUsed()
    {
        var destination = FakeHttpMessageHandler.Always(() => new HttpResponseMessage(HttpStatusCode.OK));
        var (client, provider) = CreateClientAgainst(destination);
        using var _ = provider;

        await client.GetAsync(CancellationToken.None);

        var request = destination.Requests.ShouldHaveSingleItem();
        request.AuthorizationScheme.ShouldBe("Bearer");
        request.AuthorizationParameter.ShouldBe("token-de-prueba");
    }

    [Fact]
    public async Task AddServiceClient_ReturnsTheGoodResponse_WhenTransientFailuresPrecedeIt()
    {
        var destination = FakeHttpMessageHandler.Sequence(call => call < 2
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK));
        var (client, provider) = CreateClientAgainst(destination);
        using var _ = provider;

        var response = await client.GetAsync(CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        destination.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task AddServiceClient_ReturnsTheResponseUnretried_WhenTheDestinationAnswersAClientError()
    {
        var destination = FakeHttpMessageHandler.Always(() => new HttpResponseMessage(HttpStatusCode.NotFound));
        var (client, provider) = CreateClientAgainst(destination);
        using var _ = provider;

        var response = await client.GetAsync(CancellationToken.None);

        // Un 404 es la respuesta del destino, no un fallo de red: repetirla no la cambia.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        destination.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task AddServiceClient_ThrowsHttpRequestException_WhenTheDestinationIsUnreachable()
    {
        var destination = FakeHttpMessageHandler.Always(
            () => throw new HttpRequestException("sin ruta al host"));
        var (client, provider) = CreateClientAgainst(destination);
        using var _ = provider;

        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync(CancellationToken.None));
        destination.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task AddServiceClient_FailsWithoutRetrying_WhenNoTokenCanBeObtained()
    {
        _tokenProvider.GetTokenAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new ServiceAuthenticationException("el emisor no responde"));
        var destination = FakeHttpMessageHandler.Always(() => new HttpResponseMessage(HttpStatusCode.OK));
        var (client, provider) = CreateClientAgainst(destination);
        using var _ = provider;

        await Should.ThrowAsync<ServiceAuthenticationException>(() => client.GetAsync(CancellationToken.None));

        // Quien falla es el emisor de tokens, no el destino: reintentarlo contra el destino
        // no arregla nada y contarlo como avería suya le abriría el circuito estando sano.
        destination.CallCount.ShouldBe(0);
        await _tokenProvider.Received(1).GetTokenAsync(false, Arg.Any<CancellationToken>());
    }

    #endregion
}
