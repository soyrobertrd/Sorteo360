using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Sorteo360.Platform.Http;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Http;

/// <summary>
/// Lo que hace el handler con cada petición saliente: la firma con el token del servicio y,
/// ante un 401 del destino, la repite una sola vez con un token recién pedido. Un token
/// puede quedar invalidado antes de caducar (rotación de claves del realm), y sin ese
/// reintento la llamada fallaría por algo que se arregla solo.
/// </summary>
public sealed class ClientCredentialsTokenHandlerTests
{
    private readonly IServiceTokenProvider _tokenProvider = Substitute.For<IServiceTokenProvider>();

    private HttpClient CreateClient(FakeHttpMessageHandler destination)
    {
        var handler = new ClientCredentialsTokenHandler(
            _tokenProvider,
            NullLogger<ClientCredentialsTokenHandler>.Instance)
        {
            InnerHandler = destination,
        };

        return new HttpClient(handler) { BaseAddress = new Uri("http://crm-service:8080") };
    }

    private void TokensAre(params string[] tokens)
    {
        var index = 0;
        _tokenProvider.GetTokenAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(tokens[Math.Min(index++, tokens.Length - 1)]));
    }

    [Fact]
    public async Task SendAsync_AttachesTheServiceTokenAsBearer_WhenARequestGoesOut()
    {
        TokensAre("token-1");
        var destination = FakeHttpMessageHandler.Always(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = CreateClient(destination);

        var response = await client.GetAsync("/v1/opts", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var request = destination.Requests.ShouldHaveSingleItem();
        request.AuthorizationScheme.ShouldBe("Bearer");
        request.AuthorizationParameter.ShouldBe("token-1");
    }

    [Fact]
    public async Task SendAsync_RetriesOnceWithAFreshToken_WhenTheDestinationAnswers401()
    {
        TokensAre("token-caducado", "token-nuevo");
        var destination = FakeHttpMessageHandler.Sequence(call => call == 0
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.OK));
        using var client = CreateClient(destination);

        var response = await client.GetAsync("/v1/opts", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        destination.CallCount.ShouldBe(2);
        destination.Requests[0].AuthorizationParameter.ShouldBe("token-caducado");
        destination.Requests[1].AuthorizationParameter.ShouldBe("token-nuevo");
        await _tokenProvider.Received(1).GetTokenAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ReturnsThe401_WhenTheRetryIsAlsoUnauthorized()
    {
        TokensAre("token-1", "token-2");
        var destination = FakeHttpMessageHandler.Always(
            () => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = CreateClient(destination);

        var response = await client.GetAsync("/v1/opts", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        destination.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_DoesNotRetry_WhenTheDestinationAnswers403()
    {
        TokensAre("token-1");
        var destination = FakeHttpMessageHandler.Always(
            () => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var client = CreateClient(destination);

        var response = await client.GetAsync("/v1/opts", CancellationToken.None);

        // 403 es una decisión de autorización del destino: el token es válido y otro no
        // cambiaría nada. Reintentar solo duplicaría la carga.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        destination.CallCount.ShouldBe(1);
        await _tokenProvider.DidNotReceive().GetTokenAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ResendsTheBody_WhenARequestWithContentIsRetried()
    {
        TokensAre("token-caducado", "token-nuevo");
        var destination = FakeHttpMessageHandler.Sequence(call => call == 0
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.Created));
        using var client = CreateClient(destination);

        var response = await client.PostAsync(
            "/v1/opts",
            new StringContent("""{"name":"OPT Madrid"}""", System.Text.Encoding.UTF8, "application/json"),
            CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        destination.Requests.Count.ShouldBe(2);
        destination.Requests[1].Body.ShouldBe("""{"name":"OPT Madrid"}""");
    }

    [Fact]
    public async Task SendAsync_Fails_WhenNoTokenCanBeObtained()
    {
        _tokenProvider.GetTokenAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new ServiceAuthenticationException("el emisor no responde"));
        var destination = FakeHttpMessageHandler.Always(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = CreateClient(destination);

        // Sale como HttpRequestException, así que el cliente tipado la traduce a
        // Unavailable sin distinguir si falló el emisor o el destino.
        await Should.ThrowAsync<HttpRequestException>(
            () => client.GetAsync("/v1/opts", CancellationToken.None));
        destination.CallCount.ShouldBe(0);
    }
}
