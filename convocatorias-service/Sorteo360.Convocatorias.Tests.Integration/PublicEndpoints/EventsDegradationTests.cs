using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// events-service caído, en los tres sitios que lo leen (spec <c>01</c> §6).
/// <para>
/// Tres lecturas, tres públicos, la misma respuesta: fallo duro. No hay escalera de degradación
/// acá, a diferencia del directorio de OPT, y no hay caché: una cartelera cacheada es una
/// decisión de visibilidad cacheada, que es como un evento privado sigue viéndose cinco minutos
/// después de que alguien lo escondió.
/// </para>
/// <para>
/// En su propia clase con su propia aplicación, porque tumbar al vecino abre el cortacircuitos
/// del chasis durante quince segundos y la tubería es un singleton compartido: dejarlo abierto
/// en la suite general haría fallar a las vecinas por un motivo que no es el suyo.
/// </para>
/// </summary>
public sealed class EventsDegradationTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private const string PublicEventsRoute = "/v1/public/events";

    private readonly ConvocatoriasApiDbFactory _factory;

    public EventsDegradationTests(ConvocatoriasApiDbFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
    }

    [Fact]
    public async Task PublicList_Returns503EventsUnavailable_WhenEventsIsDown()
    {
        _factory.Events.GoDown();

        var response = await _factory.CreateClient().GetAsync(PublicEventsRoute);

        await ShouldBeEventsUnavailableAsync(response);
    }

    [Fact]
    public async Task PublicDetail_Returns503EventsUnavailable_WhenEventsIsDown()
    {
        _factory.Events.GoDown();

        var response = await _factory.CreateClient().GetAsync($"{PublicEventsRoute}/{EventsStub.EventId}");

        await ShouldBeEventsUnavailableAsync(response);
    }

    /// <summary>
    /// Lo que el rechazo protege: nadie recibe un correo con un hueco donde va el nombre del
    /// evento, y la campaña se queda en borrador, sin sello de lanzamiento.
    /// </summary>
    [Fact]
    public async Task Launch_Returns503AndLeavesTheCampaignInDraft_WhenEventsIsDown()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);
        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var selected = await CampaignTestClient.GetAsync(client, campaign.Id);

        _factory.Events.GoDown();

        var response = await CampaignTestClient.LaunchAsync(
            client, campaign.Id, new { expectedRecipientCount = selected.RecipientCount });

        await ShouldBeEventsUnavailableAsync(response);

        _factory.Events.ServeDefaults();
        var afterwards = await CampaignTestClient.GetAsync(client, campaign.Id);
        afterwards.State.ShouldBe(Core.Campaigns.CampaignState.Draft);
        afterwards.LaunchedAt.ShouldBeNull();
        afterwards.LaunchedById.ShouldBeNull();
    }

    private static async Task ShouldBeEventsUnavailableAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe(EventsUnavailableError.ErrorCode);
    }
}
