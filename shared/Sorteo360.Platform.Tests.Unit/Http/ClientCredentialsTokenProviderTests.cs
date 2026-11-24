using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Sorteo360.Platform.Auth;
using Sorteo360.Platform.Http;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Http;

/// <summary>
/// Obtención del token propio del servicio por client credentials: se pide una sola vez
/// mientras siga vigente (pedirlo en cada petición saliente convertiría a Keycloak en el
/// cuello de botella de toda llamada entre servicios), se renueva antes de caducar, y un
/// fallo del emisor se distingue del fallo del servicio destino.
/// </summary>
public sealed class ClientCredentialsTokenProviderTests
{
    private const string Authority = "http://keycloak:8080/realms/pgti360";
    private const string ClientId = "events-service";
    private const string ClientSecret = "un-secreto";
    private const string ExpectedTokenEndpoint = $"{Authority}/protocol/openid-connect/token";

    private readonly TestTimeProvider _time = new();

    private static string TokenJson(string accessToken, int expiresInSeconds) =>
        $$"""
          {"access_token":"{{accessToken}}","expires_in":{{expiresInSeconds}},"token_type":"Bearer"}
          """;

    private static FakeHttpMessageHandler NumberedTokens(TimeSpan? responseDelay = null) =>
        FakeHttpMessageHandler.Sequence(
            call => FakeHttpMessageHandler.Json(HttpStatusCode.OK, TokenJson($"token-{call}", 300)),
            responseDelay);

    private ClientCredentialsTokenProvider CreateProvider(
        FakeHttpMessageHandler handler,
        string authority = Authority)
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(ClientCredentialsTokenProvider.HttpClientName)
            .Returns(_ => new HttpClient(handler, disposeHandler: false));

        return new ClientCredentialsTokenProvider(
            httpClientFactory,
            new KeycloakSettings { Authority = authority, Audience = "events-api" },
            new ServiceAuthSettings { ClientId = ClientId, ClientSecret = ClientSecret },
            _time,
            NullLogger<ClientCredentialsTokenProvider>.Instance);
    }

    #region Petición al emisor

    [Fact]
    public async Task GetTokenAsync_PostsClientCredentialsToTheRealm_WhenTheCacheIsCold()
    {
        var handler = NumberedTokens();
        var provider = CreateProvider(handler);

        var token = await provider.GetTokenAsync(forceRefresh: false, CancellationToken.None);

        token.ShouldBe("token-0");
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe(ExpectedTokenEndpoint);
        var body = request.Body.ShouldNotBeNull();
        body.ShouldContain("grant_type=client_credentials");
        body.ShouldContain($"client_id={ClientId}");
        body.ShouldContain($"client_secret={ClientSecret}");
    }

    [Fact]
    public async Task GetTokenAsync_BuildsTheTokenEndpoint_WhenTheAuthorityHasATrailingSlash()
    {
        var handler = NumberedTokens();
        var provider = CreateProvider(handler, authority: $"{Authority}/");

        await provider.GetTokenAsync(forceRefresh: false, CancellationToken.None);

        handler.Requests.ShouldHaveSingleItem().RequestUri!.ToString().ShouldBe(ExpectedTokenEndpoint);
    }

    #endregion

    #region Caché

    [Fact]
    public async Task GetTokenAsync_ReturnsTheCachedToken_WhenItIsStillValid()
    {
        var handler = NumberedTokens();
        var provider = CreateProvider(handler);

        var first = await provider.GetTokenAsync(forceRefresh: false, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(2));
        var second = await provider.GetTokenAsync(forceRefresh: false, CancellationToken.None);

        first.ShouldBe("token-0");
        second.ShouldBe("token-0");
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetTokenAsync_RequestsANewToken_WhenTheCachedOneIsWithinTheExpiryLeeway()
    {
        var handler = NumberedTokens();
        var provider = CreateProvider(handler);

        await provider.GetTokenAsync(forceRefresh: false, CancellationToken.None);
        // El token dura 300 s y se renueva con antelación: a los 280 s ya toca pedirlo.
        _time.Advance(TimeSpan.FromSeconds(280));
        var renewed = await provider.GetTokenAsync(forceRefresh: false, CancellationToken.None);

        renewed.ShouldBe("token-1");
        handler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetTokenAsync_RequestsANewToken_WhenRefreshIsForced()
    {
        var handler = NumberedTokens();
        var provider = CreateProvider(handler);

        await provider.GetTokenAsync(forceRefresh: false, CancellationToken.None);
        var refreshed = await provider.GetTokenAsync(forceRefresh: true, CancellationToken.None);

        refreshed.ShouldBe("token-1");
        handler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetTokenAsync_RequestsTheTokenOnce_WhenSeveralCallersRaceOnAColdCache()
    {
        var handler = NumberedTokens(responseDelay: TimeSpan.FromMilliseconds(50));
        var provider = CreateProvider(handler);

        var tokens = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            provider.GetTokenAsync(forceRefresh: false, CancellationToken.None)));

        tokens.ShouldAllBe(token => token == "token-0");
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetTokenAsync_RenewsOnce_WhenSeveralCallersForceRefreshAtTheSameTime()
    {
        var handler = NumberedTokens(responseDelay: TimeSpan.FromMilliseconds(50));
        var provider = CreateProvider(handler);
        await provider.GetTokenAsync(forceRefresh: false, CancellationToken.None);

        // Una ráfaga de 401 (p. ej. tras rotar las claves del realm) no debe traducirse en
        // una ráfaga de renovaciones: quien llega tarde aprovecha la del primero.
        var tokens = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            provider.GetTokenAsync(forceRefresh: true, CancellationToken.None)));

        tokens.ShouldAllBe(token => token == "token-1");
        handler.CallCount.ShouldBe(2);
    }

    #endregion

    #region Fallos del emisor

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetTokenAsync_Throws_WhenTheTokenEndpointFails(HttpStatusCode statusCode)
    {
        var handler = FakeHttpMessageHandler.Always(
            () => FakeHttpMessageHandler.Json(statusCode, """{"error":"invalid_client"}"""));
        var provider = CreateProvider(handler);

        var exception = await Should.ThrowAsync<ServiceAuthenticationException>(
            () => provider.GetTokenAsync(forceRefresh: false, CancellationToken.None));

        exception.Message.ShouldContain(ClientId);
        exception.Message.ShouldNotContain(ClientSecret);
    }

    [Fact]
    public async Task GetTokenAsync_Throws_WhenTheResponseCarriesNoAccessToken()
    {
        var handler = FakeHttpMessageHandler.Always(
            () => FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"expires_in":300}"""));
        var provider = CreateProvider(handler);

        await Should.ThrowAsync<ServiceAuthenticationException>(
            () => provider.GetTokenAsync(forceRefresh: false, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_Throws_WhenTheIssuerIsUnreachable()
    {
        var handler = FakeHttpMessageHandler.Always(
            () => throw new HttpRequestException("sin ruta al host"));
        var provider = CreateProvider(handler);

        await Should.ThrowAsync<ServiceAuthenticationException>(
            () => provider.GetTokenAsync(forceRefresh: false, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_RecoversOnTheNextCall_WhenAFetchFails()
    {
        var handler = FakeHttpMessageHandler.Sequence(call => call == 0
            ? FakeHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}")
            : FakeHttpMessageHandler.Json(HttpStatusCode.OK, TokenJson("token-ok", 300)));
        var provider = CreateProvider(handler);

        await Should.ThrowAsync<ServiceAuthenticationException>(
            () => provider.GetTokenAsync(forceRefresh: false, CancellationToken.None));
        var token = await provider.GetTokenAsync(forceRefresh: false, CancellationToken.None);

        token.ShouldBe("token-ok");
    }

    #endregion
}
