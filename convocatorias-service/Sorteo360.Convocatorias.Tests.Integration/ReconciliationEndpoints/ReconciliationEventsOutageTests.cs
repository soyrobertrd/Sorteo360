using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.ReconciliationEndpoints;

/// <summary>
/// Solo events-service sabe qué eventos se cancelaron, así que caído se lleva un conteo y no la
/// pantalla. Clase y fábrica propias: el circuito del chasis queda abierto quince segundos y
/// tumbaría a la clase de al lado por una caída que no es suya.
/// </summary>
public sealed class ReconciliationEventsOutageTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public ReconciliationEventsOutageTests(RegistrationPortalFactory factory)
    {
        _factory = factory;
        _factory.Events.GoDown();
    }

    [Fact]
    public async Task Summary_ReturnsANullUncancelledNoticesAndTheOtherCounts_WhenEventsServiceIsDown()
    {
        var response = await _factory.CreateClient().GetAsync("/v1/convocatorias/reconciliation");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("uncancelledNotices").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("pendingRegistrationSync").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task UncancelledNotices_Returns503_WhenEventsServiceIsDown()
    {
        var response = await _factory.CreateClient().GetAsync("/v1/convocatorias/reconciliation/uncancelled-notices");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .ShouldBe(EventsUnavailableError.ErrorCode);
    }
}
