using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Notifications.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Integration.History;

/// <summary>
/// El historial no escribe nada, y no por recorte de alcance: sin los valores de <c>data</c> un
/// envío con marcadores no se puede reconstruir, así que un reenvío mandaría un correo distinto del
/// que la pantalla muestra.
/// <para>
/// Se recorre la tabla de rutas registrada y no una lista escrita a mano: así un verbo que alguien
/// agregue mañana rompe este test en vez de pasar desapercibido.
/// </para>
/// </summary>
public sealed class HistoryIsReadOnlyTests : IClassFixture<NotificationsApiFactory>
{
    /// <summary>
    /// Las dos únicas escrituras del servicio, y ninguna es del historial: aceptar un envío y
    /// recibir un lote de eventos del proveedor. Enumerarlas acá es lo que hace que una tercera
    /// rompa este test en vez de aparecer callada.
    /// </summary>
    private static readonly string[] WriteEndpoints =
    [
        "ReceiveSendGridEvents",
        "SendNotification"
    ];

    private static readonly string[] ReadEndpoints =
    [
        "ListNotificationHistory",
        "ListOrphanNotificationEvents",
        "GetNotificationBatch"
    ];

    private readonly NotificationsApiFactory _factory;

    public HistoryIsReadOnlyTests(NotificationsApiFactory factory) => _factory = factory;

    [Fact]
    public void HistoryRoutes_ReturnsOnlyGet_WhenTheirVerbsAreListed()
    {
        var endpoints = RouteEndpoints();

        foreach (var name in ReadEndpoints)
        {
            var endpoint = endpoints.SingleOrDefault(item => item.DisplayName?.Contains(name, StringComparison.Ordinal) == true
                || item.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);

            endpoint.ShouldNotBeNull($"No existe la ruta {name}.");

            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .ShouldBe([HttpMethods.Get], $"La ruta {name} admite algo más que GET.");
        }
    }

    [Fact]
    public void MappedWrites_ReturnsOnlyTheSendEndpointAndTheEventWebhook_WhenEveryRouteIsListed()
    {
        var writers = RouteEndpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is { } methods
                && methods.HttpMethods.Any(method =>
                    method is not (null or "GET" or "HEAD" or "OPTIONS")))
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                ?? endpoint.DisplayName)
            .ToArray();

        writers.Order(StringComparer.Ordinal).ShouldBe(WriteEndpoints);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task BatchRoute_Returns405_WhenCalledWithAWriteVerb(string method)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), $"/v1/notifications/batches/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new { })
        };

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task OrphanRoute_Returns405_WhenCalledWithAWriteVerb(string method)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), "/v1/notifications/orphan-events")
        {
            Content = JsonContent.Create(new { })
        };

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    private RouteEndpoint[] RouteEndpoints() =>
        [.. _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];
}
