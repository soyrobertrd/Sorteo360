using System.Net;
using Sorteo360.Platform.Http;
using Polly;
using Polly.CircuitBreaker;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Http;

/// <summary>
/// El handler traduce lo que lanza Polly a <see cref="HttpRequestException"/>. Es lo que
/// mantiene la frontera del chasis: quien escribe un cliente tipado captura un solo tipo y
/// nunca nombra a Polly para decidir que su dependencia no está disponible.
/// </summary>
public sealed class ServiceResilienceHandlerTests
{
    private const string ServiceName = "Crm";

    private static HttpClient CreateClient(
        ResiliencePipeline<HttpResponseMessage> pipeline,
        HttpMessageHandler destination)
    {
        var handler = new ServiceResilienceHandler(pipeline, ServiceName)
        {
            InnerHandler = destination,
        };

        return new HttpClient(handler) { BaseAddress = new Uri("http://crm-service:8080") };
    }

    [Fact]
    public async Task SendAsync_ThrowsHttpRequestException_WhenTheAttemptTimesOut()
    {
        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(TimeSpan.FromMilliseconds(50))
            .Build();
        var destination = FakeHttpMessageHandler.Always(
            () => new HttpResponseMessage(HttpStatusCode.OK),
            responseDelay: TimeSpan.FromSeconds(5));
        using var client = CreateClient(pipeline, destination);

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => client.GetAsync("/v1/opts", CancellationToken.None));

        exception.Message.ShouldContain(ServiceName);
        exception.ShouldNotBeOfType<ServiceAuthenticationException>();
    }

    [Fact]
    public async Task SendAsync_ThrowsHttpRequestException_WhenTheCircuitIsOpen()
    {
        var manualControl = new CircuitBreakerManualControl();
        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                ManualControl = manualControl,
            })
            .Build();
        await manualControl.IsolateAsync(CancellationToken.None);
        var destination = FakeHttpMessageHandler.Always(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = CreateClient(pipeline, destination);

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => client.GetAsync("/v1/opts", CancellationToken.None));

        exception.Message.ShouldContain(ServiceName);
        // Con el circuito abierto no se molesta al destino: de eso trata cortar.
        destination.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_ReturnsTheResponse_WhenThePipelineDoesNotInterfere()
    {
        var pipeline = ResiliencePipeline<HttpResponseMessage>.Empty;
        var destination = FakeHttpMessageHandler.Always(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = CreateClient(pipeline, destination);

        var response = await client.GetAsync("/v1/opts", CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        destination.CallCount.ShouldBe(1);
    }
}
