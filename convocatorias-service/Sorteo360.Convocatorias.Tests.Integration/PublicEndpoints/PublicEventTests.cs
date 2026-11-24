using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// La cartelera pública (spec <c>01</c> §5): lo que un desconocido ve de un evento de MITUR, y
/// sobre todo lo que no ve.
/// <para>
/// Se ejercita la cadena entera contra un events-service simulado por HTTP, no un doble de
/// <c>IEventDirectory</c>: lo que hay que cobrar es que el recorte ocurra, y un doble que ya
/// devuelve el tipo recortado no puede fallar en eso.
/// </para>
/// <para>
/// Con base de verdad porque las dos rutas leen además la ventana de inscripción (spec <c>06</c>
/// §5), que es tabla de este servicio.
/// </para>
/// </summary>
public sealed class PublicEventTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private const string ListRoute = "/v1/public/events";

    private readonly ConvocatoriasApiDbFactory _factory;

    public PublicEventTests(ConvocatoriasApiDbFactory factory)
    {
        _factory = factory;
        _factory.Events.ServeDefaults();
    }

    #region El listado

    [Fact]
    public async Task List_Returns200_WhenTheCallerIsAnonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(ListRoute);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_ReturnsOnlyPublicAndUnfinishedEvents_WhenEventsAreMixed()
    {
        var publicEvent = Event("Roadshow Madrid", EventsStub.PublicVisibility, StubEventStatus.InProgress);
        _factory.Events.ServeEvents(
            publicEvent,
            Event("Gala privada", EventsStub.PrivateVisibility, StubEventStatus.InProgress),
            Event("Roadshow del año pasado", EventsStub.PublicVisibility, StubEventStatus.Closed),
            Event("Roadshow suspendido", EventsStub.PublicVisibility, StubEventStatus.Cancelled));

        var items = await ListAsync();

        items.Length.ShouldBe(1);
        items[0].GetProperty("id").GetGuid().ShouldBe(publicEvent.Id);
    }

    /// <summary>
    /// El aforo previsto es el objetivo interno de la OPT, no una capacidad, y el presupuesto no
    /// es de nadie fuera de MITUR. Ninguno de los dos existe en esta respuesta, ni siquiera nulo.
    /// </summary>
    [Fact]
    public async Task List_ReturnsNoInternalNumbers_WhenItServes()
    {
        var items = await ListAsync();

        var item = items.ShouldHaveSingleItem();
        ShouldNotCarryInternalFields(item);
    }

    [Fact]
    public async Task List_ReturnsTheCityNamesWithoutIds_WhenItServes()
    {
        var items = await ListAsync();

        var cities = items.ShouldHaveSingleItem().GetProperty("cities");
        cities.GetArrayLength().ShouldBe(1);
        cities[0].GetProperty("name").GetString().ShouldBe("Madrid");
        // Sin el id de la ciudad: a un desconocido no le sirve para nada.
        cities[0].TryGetProperty("id", out _).ShouldBeFalse();
    }

    #endregion

    #region El detalle

    [Fact]
    public async Task Detail_ReturnsTheEventWithItsOrganizer_WhenTheEventIsPublic()
    {
        var response = await _factory.CreateClient().GetAsync($"{ListRoute}/{EventsStub.EventId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().ShouldBe(EventsStub.EventName);
        body.GetProperty("organizer").GetString().ShouldBe(EventsStub.OptName);
        body.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
        ShouldNotCarryInternalFields(body);
    }

    /// <summary>
    /// Un evento terminado es historia pública: un enlace compartido la semana pasada tiene que
    /// seguir resolviendo después de que el evento ocurra.
    /// </summary>
    [Fact]
    public async Task Detail_Returns200_WhenTheEventIsClosed()
    {
        var closed = Event("Roadshow del año pasado", EventsStub.PublicVisibility, StubEventStatus.Closed);
        _factory.Events.ServeEvents(closed);

        var response = await _factory.CreateClient().GetAsync($"{ListRoute}/{closed.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(EventsStub.PrivateVisibility, StubEventStatus.InProgress)]
    [InlineData(EventsStub.PublicVisibility, StubEventStatus.Cancelled)]
    public async Task Detail_Returns404WithItsCode_WhenTheEventIsPrivateOrCancelled(string visibility, string status)
    {
        var hidden = Event("Evento escondido", visibility, status);
        _factory.Events.ServeEvents(hidden);

        var response = await _factory.CreateClient().GetAsync($"{ListRoute}/{hidden.Id}");

        await ShouldBeTheOneNotFoundCodeAsync(response);
    }

    /// <summary>
    /// Un id desconocido responde exactamente igual que uno privado: decir cuál de los dos es le
    /// confirmaría a un desconocido que el evento existe y que MITUR lo escondió.
    /// </summary>
    [Fact]
    public async Task Detail_Returns404WithTheSameCode_WhenTheIdIsUnknown()
    {
        var response = await _factory.CreateClient().GetAsync($"{ListRoute}/{Guid.NewGuid()}");

        await ShouldBeTheOneNotFoundCodeAsync(response);
    }

    /// <summary>
    /// El directorio del CRM caído deja el nombre de la OPT nulo. La página omite el bloque de
    /// organizador y sigue sirviendo: un 200 sin organizador, no un 503 (spec <c>01</c> §6).
    /// </summary>
    [Fact]
    public async Task Detail_Returns200WithANullOrganizer_WhenTheOptDirectoryIsDegraded()
    {
        var degraded = Event("Roadshow sin organizador", EventsStub.PublicVisibility, StubEventStatus.InProgress)
            with { OptName = null };
        _factory.Events.ServeEvents(degraded);

        var response = await _factory.CreateClient().GetAsync($"{ListRoute}/{degraded.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("organizer").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("name").GetString().ShouldBe("Roadshow sin organizador");
    }

    #endregion

    private static StubEvent Event(string name, string visibility, string status) =>
        new(Guid.NewGuid(), name, visibility, EventsStub.EventLanguage, status);

    private async Task<JsonElement[]> ListAsync()
    {
        var response = await _factory.CreateClient().GetAsync(ListRoute);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    private static async Task ShouldBeTheOneNotFoundCodeAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe(PublicEventNotFoundError.ErrorCode);
    }

    /// <summary>
    /// Los campos que se quedan dentro de events-service. Ausentes, no nulos: un nulo es un campo
    /// que existe en el contrato, y estos no existen.
    /// </summary>
    private static void ShouldNotCarryInternalFields(JsonElement body)
    {
        foreach (var field in new[] { "invitedCount", "estimatedBudget", "totalCost", "optContact", "mapEmbedUrl" })
            body.TryGetProperty(field, out _).ShouldBeFalse($"El portal no debe publicar '{field}'.");
    }
}
