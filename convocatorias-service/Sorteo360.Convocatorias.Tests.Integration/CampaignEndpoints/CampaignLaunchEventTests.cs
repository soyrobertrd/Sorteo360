using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// El paso 4 del lanzamiento: leer el evento y negarse por lo que diga (spec <c>04</c> §1,
/// spec <c>02</c> §2.2, spec <c>06</c> §4.6).
/// <para>
/// <b>Esta lectura es la única guarda que hay.</b> events-service nunca llama a convocatorias,
/// así que nada más dentro de este servicio puede enterarse de que el evento se canceló hace una
/// hora, y escribirle a varios cientos de personas por él es exactamente el fallo que este paso
/// existe para impedir.
/// </para>
/// <para>
/// El corte de events-service vive en <see cref="CampaignLaunchEventsOutageTests"/>, con su
/// propia aplicación: el cortacircuitos del chasis es un singleton y un vecino caído no tiene
/// por qué hacer fallar a las pruebas vecinas.
/// </para>
/// </summary>
public sealed class CampaignLaunchEventTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public CampaignLaunchEventTests(ConvocatoriasApiDbFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
    }

    #region El evento cancelado

    [Fact]
    public async Task LaunchCampaign_Returns409EventCancelledAndLeavesTheDraft_WhenTheEventIsCancelled()
    {
        var eventId = Guid.NewGuid();
        var (client, campaign, count) = await CampaignForAsync(Cancelled(eventId));

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await ProblemOfAsync(response);

        problem["code"].ToString().ShouldBe(CampaignEventCancelledError.ErrorCode);
        problem["eventId"].ToString().ShouldBe(eventId.ToString());

        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
    }

    /// <summary>
    /// Un evento cancelado <b>y</b> sin recorrido se rechaza por cancelado. A un evento apagado
    /// le sobra que además le falte el lugar, y "el evento está cancelado" es lo que quien lanza
    /// tiene que leer.
    /// </summary>
    [Fact]
    public async Task LaunchCampaign_Returns409EventCancelled_WhenTheCancelledEventAlsoHasNoCities()
    {
        var eventId = Guid.NewGuid();
        var (client, campaign, count) = await CampaignForAsync(Cancelled(eventId) with { Cities = [] });

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemOfAsync(response))["code"].ToString()
            .ShouldBe(CampaignEventCancelledError.ErrorCode);
    }

    /// <summary>
    /// Un evento cerrado no se rechaza. Cerrar significa que el evento ocurrió, que es otra cosa
    /// que cancelarlo, y solo lo segundo bloquea el lanzamiento (spec <c>06</c> §4.6).
    /// </summary>
    [Fact]
    public async Task LaunchCampaign_Returns202_WhenTheEventIsClosed()
    {
        var eventId = Guid.NewGuid();
        var (client, campaign, count) = await CampaignForAsync(
            Alive(eventId) with { Status = StubEventStatus.Closed });

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    #endregion

    #region La ventana de inscripción

    /// <summary>
    /// Paso 5 (spec <c>04</c> §1, spec <c>06</c> §5.5): nadie quiere mandarle a varios cientos de
    /// personas un enlace que contesta <c>409</c>. Quien quiera invitar a más reabre primero.
    /// </summary>
    [Fact]
    public async Task LaunchCampaign_Returns409CampaignRegistrationsClosedAndHandsOffNobody_WhenTheWindowIsShut()
    {
        var eventId = Guid.NewGuid();
        var (client, campaign, count) = await CampaignForAsync(Alive(eventId));
        (await client.PostAsync($"/v1/convocatorias/events/{eventId}/close-registrations", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemOfAsync(response))["code"].ToString()
            .ShouldBe(CampaignRegistrationsClosedError.ErrorCode);

        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);
    }

    #endregion

    #region El valor que falta

    /// <summary>
    /// <c>{{lugar}}</c> sale de las ciudades del recorrido (spec <c>01</c> §4.1), así que un
    /// evento sin ninguna lo deja vacío. El rechazo nombra el campo, para que nadie mande "se
    /// celebrará en ." a mil personas.
    /// </summary>
    [Fact]
    public async Task LaunchCampaign_Returns400NamingLugar_WhenTheEventHasNoCities()
    {
        var eventId = Guid.NewGuid();
        var (client, campaign, count) = await CampaignForAsync(Alive(eventId) with { Cities = [] });

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ProblemOfAsync(response);

        problem["code"].ToString().ShouldBe(EventValuesMissingError.ErrorCode);
        FieldsOf(problem).ShouldBe([SupportedPlaceholder.EventLocation]);

        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
    }

    /// <summary>
    /// Un id que events-service no conoce cae en el mismo rechazo, nombrando los tres: sin evento
    /// no hay ni nombre, ni lugar, ni fecha que poner en el correo.
    /// </summary>
    [Fact]
    public async Task LaunchCampaign_Returns400NamingAllThreeValues_WhenEventsServiceDoesNotKnowTheEvent()
    {
        // No se publica ningún evento con este id, así que events-service contesta 404.
        var (client, campaign, count) = await CampaignForAsync(Alive(Guid.NewGuid()), convoking: Guid.NewGuid());

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await ProblemOfAsync(response);

        problem["code"].ToString().ShouldBe(EventValuesMissingError.ErrorCode);
        FieldsOf(problem).ShouldBe(SupportedPlaceholder.EventLevel, ignoreOrder: true);

        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
    }

    #endregion

    #region El sitio de la lectura en el orden

    /// <summary>
    /// El evento se lee <b>después</b> de la recomprobación del CRM (spec <c>04</c> §1). Lo que
    /// se mira es la hora de llegada a cada vecino y no una cuenta: son dos servidores con dos
    /// registros, así que ningún índice dentro de uno dice nada del otro.
    /// </summary>
    [Fact]
    public async Task LaunchCampaign_Returns202WithTheEventReadAfterTheCrmRecheck_WhenTheEventIsAlive()
    {
        var eventId = Guid.NewGuid();
        var (client, campaign, count) = await CampaignForAsync(Alive(eventId));

        _factory.Crm.ResetRequestLog();
        _factory.Events.ResetRequestLog();

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var mailability = _factory.Crm.FirstArrivalEndingIn(CrmStub.MailabilityPath);
        var eventRead = _factory.Events.FirstArrivalEndingIn(eventId.ToString());

        mailability.ShouldNotBeNull();
        eventRead.ShouldNotBeNull();
        mailability.Value.ShouldBeLessThan(eventRead.Value);
    }

    /// <summary>
    /// Y <b>antes</b> del salto a <c>sending</c>: el evento se rechaza, así que la campaña sigue
    /// en borrador aunque el CRM ya haya contestado que a todos se les puede escribir.
    /// </summary>
    [Fact]
    public async Task LaunchCampaign_ReturnsAnUnlaunchedDraftAfterTheCrmRecheck_WhenTheEventIsCancelled()
    {
        var eventId = Guid.NewGuid();
        var (client, campaign, count) = await CampaignForAsync(Cancelled(eventId));

        _factory.Crm.ResetRequestLog();

        await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = count });

        // El CRM se gastó entero y la campaña no se movió: la lectura del evento corre entre las
        // dos cosas.
        _factory.Crm.MailabilityCallCount.ShouldBe(1);
        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);
    }

    #endregion

    #region La respuesta

    /// <summary>
    /// El 202 apunta a la campaña. El cuerpo de esa respuesta es de ese instante y deja de ser
    /// cierto en cuanto el worker avanza, así que lo que importa es a dónde volver a mirar.
    /// </summary>
    [Fact]
    public async Task LaunchCampaign_Returns202WithTheCampaignLocation_WhenAccepted()
    {
        var (client, campaign, count) = await CampaignForAsync(Alive(Guid.NewGuid()));

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location!.ToString().ShouldBe($"{CampaignTestClient.Route}/{campaign.Id}");
    }

    #endregion

    /// <summary>Un evento público que todavía no terminó: el que no objeta nada.</summary>
    private static StubEvent Alive(Guid id) => new(
        id,
        EventsStub.EventName,
        EventsStub.PublicVisibility,
        EventsStub.EventLanguage,
        StubEventStatus.InProgress);

    /// <summary>El mismo evento, apagado.</summary>
    private static StubEvent Cancelled(Guid id) => Alive(id) with { Status = StubEventStatus.Cancelled };

    /// <summary>
    /// Publica ese evento y arma un borrador con destinatarios contactables que lo convoca.
    /// <paramref name="convoking"/> deja apuntar la campaña a <b>otro</b> id, que es cómo se
    /// llega a "events-service no conoce el evento".
    /// </summary>
    private async Task<(HttpClient Client, CampaignResponse Campaign, int Count)> CampaignForAsync(
        StubEvent published,
        Guid? convoking = null)
    {
        _factory.Events.ServeEvents(published);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, convoking ?? published.Id);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withRecipients = await CampaignTestClient.GetAsync(client, campaign.Id);

        withRecipients.RecipientCount.ShouldBeGreaterThan(0);

        return (client, withRecipients, withRecipients.RecipientCount);
    }

    /// <summary>Un rechazo no escribe nada: ni el salto de estado, ni los sellos.</summary>
    private static async Task ShouldStillBeAnUnlaunchedDraftAsync(HttpClient client, Guid id)
    {
        var campaign = await CampaignTestClient.GetAsync(client, id);

        campaign.State.ShouldBe(CampaignState.Draft);
        campaign.LaunchedById.ShouldBeNull();
        campaign.LaunchedAt.ShouldBeNull();
        campaign.SentAt.ShouldBeNull();
    }

    /// <summary>El ProblemDetails de la respuesta, campo a campo.</summary>
    private static async Task<Dictionary<string, object>> ProblemOfAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem.ShouldNotBeNull();

        return problem;
    }

    /// <summary>Los campos que nombra la extensión <c>fields</c> del rechazo.</summary>
    private static IReadOnlyList<string> FieldsOf(Dictionary<string, object> problem) =>
    [
        .. ((JsonElement)problem["fields"])
            .EnumerateArray()
            .Select(field => field.GetString()!),
    ];
}
