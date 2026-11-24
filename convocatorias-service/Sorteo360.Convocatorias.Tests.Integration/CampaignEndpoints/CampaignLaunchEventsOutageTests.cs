using System.Net;
using System.Net.Http.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// events-service se cae en el paso 4 del lanzamiento (spec <c>01</c> §6). No hay escalera de
/// degradación y no hay caché: sin el evento no sale ningún correo, porque saldría con un hueco
/// donde va su nombre.
/// <para>
/// Lo que además se cobra acá es <b>el orden, sin mirar ningún reloj</b>: con events-service
/// caído los dos pasos del CRM ya ocurrieron, lo que solo es posible si la lectura del evento
/// corre detrás de ellos.
/// </para>
/// <para>
/// Va en su propia clase porque un vecino caído deja huella: el chasis reintenta y su
/// cortacircuitos es un singleton compartido. Cada clase de test trae su propia aplicación, así
/// que acá el circuito nace cerrado y muere con ella.
/// </para>
/// </summary>
public sealed class CampaignLaunchEventsOutageTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private static readonly StubContact Ana =
        new(Guid.NewGuid(), "Ana", "Pérez", "ana.events@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private static readonly StubContact Luis =
        new(Guid.NewGuid(), "Luis", "Gómez", "luis.events@acme.com", CanBeEmailed: true, SuppressionReason: null);

    private readonly ConvocatoriasApiDbFactory _factory;

    public CampaignLaunchEventsOutageTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task Launch_Returns503AndLaunchesNobody_WhenEventsServiceIsDown()
    {
        var (client, campaign, count) = await CampaignWithRecipientsAsync();

        _factory.Events.GoDown();

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem.ShouldNotBeNull();
        problem["code"].ToString().ShouldBe(EventsUnavailableError.ErrorCode);

        var stillDraft = await CampaignTestClient.GetAsync(client, campaign.Id);

        stillDraft.State.ShouldBe(CampaignState.Draft);
        stillDraft.LaunchedById.ShouldBeNull();
        stillDraft.LaunchedAt.ShouldBeNull();

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);
    }

    /// <summary>
    /// La prueba del orden que no depende de un reloj: si el evento se leyera primero, este 503
    /// llegaría <b>sin</b> que el CRM hubiera recibido nada.
    /// </summary>
    [Fact]
    public async Task Launch_Returns503AfterTheCrmStepsRan_WhenTheEventReadFails()
    {
        var (client, campaign, count) = await CampaignWithRecipientsAsync();

        await CampaignTestClient.UnsubscribeRecipientAsync(_factory, campaign.Id, Ana.Id);

        _factory.Events.GoDown();
        _factory.Crm.ResetRequestLog();

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var calls = _factory.Crm.CallsSinceReset;

        calls.ShouldContain(call => call.EndsWith("/opt-out", StringComparison.Ordinal));
        calls.ShouldContain(call => call.EndsWith(CrmStub.MailabilityPath, StringComparison.Ordinal));
    }

    /// <summary>Un borrador con dos destinatarios contactables que convoca el evento simulado.</summary>
    private async Task<(HttpClient Client, CampaignResponse Campaign, int Count)> CampaignWithRecipientsAsync()
    {
        _factory.Crm.ServeContacts(Ana, Luis);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withRecipients = await CampaignTestClient.GetAsync(client, campaign.Id);

        withRecipients.RecipientCount.ShouldBe(2);

        return (client, withRecipients, withRecipients.RecipientCount);
    }
}
