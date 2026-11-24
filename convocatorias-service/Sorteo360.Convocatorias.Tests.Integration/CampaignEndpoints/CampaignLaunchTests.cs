using System.Net;
using System.Net.Http.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// El lanzamiento (spec <c>03</c> §4 y §5.1). Una sola llamada, un solo rol, y ninguna
/// aprobación.
/// <para>
/// Lo que más se cobra acá es que <b>un lanzamiento fallido no escribe nada</b>: cada rechazo
/// deja la campaña en <c>draft</c>, sin <c>launchedBy</c>, sin <c>launchedAt</c> y con sus filas
/// intactas. No hay registro de lanzamiento fallido que leer, porque una campaña todavía en
/// borrador ya es el registro.
/// </para>
/// </summary>
public sealed class CampaignLaunchTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public CampaignLaunchTests(ConvocatoriasApiDbFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
    }

    #region El lanzamiento que sí procede

    [Fact]
    public async Task Launch_Returns202StampingWhoAndWhenWithSentAtNull_WhenTheCountMatches()
    {
        var client = _factory.CreateClient();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        var response = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var launched = await CampaignTestClient.GetAsync(client, campaign.Id);

        launched.State.ShouldBe(CampaignState.Sending);
        launched.LaunchedById.ShouldBe(TestAuthHandler.DefaultUserId);
        launched.LaunchedAt.ShouldNotBeNull();
        launched.SentAt.ShouldBeNull();
    }

    /// <summary>
    /// Las filas no se tocan al lanzar. La entrega corre después de contestar, así que todo
    /// sigue <c>pending</c> justo después del <c>202</c>.
    /// </summary>
    [Fact]
    public async Task Launch_ReturnsEveryRecipientStillPending_WhenJustLaunched()
    {
        var client = _factory.CreateClient();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = count });

        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);
    }

    [Fact]
    public async Task Launch_Returns409TransitionNotAllowed_WhenTheCampaignIsAlreadySending()
    {
        var client = _factory.CreateClient();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = count });

        var again = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = count });

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldCarryCodeAsync(again, "CAMPAIGN_TRANSITION_NOT_ALLOWED");
    }

    #endregion

    #region Los tres rechazos

    [Fact]
    public async Task Launch_Returns400HasNoRecipientsAndLeavesTheDraft_WhenThereAreNoRecipientRows()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldCarryCodeAsync(response, "CAMPAIGN_HAS_NO_RECIPIENTS");
        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
    }

    /// <summary>
    /// Dos códigos y no uno: "nunca seleccionaste a nadie" y "los que seleccionaste están todos
    /// dados de baja" son dos errores distintos, y juntarlos mandaría a quien coordina a buscar
    /// una lista que está ahí mismo.
    /// </summary>
    [Fact]
    public async Task Launch_Returns400HasNoMailableRecipients_WhenEveryRecipientIsUnmailable()
    {
        var client = _factory.CreateClient();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        await CampaignTestClient.UnsubscribeEveryRecipientAsync(_factory, campaign.Id);

        var response = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldCarryCodeAsync(response, "CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS");
        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
    }

    [Fact]
    public async Task Launch_Returns409CarryingTheRealCount_WhenTheCountIsStale()
    {
        var client = _factory.CreateClient();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        var response = await CampaignTestClient.LaunchAsync(
            client,
            campaign.Id,
            new { expectedRecipientCount = count + 13 });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem!["code"].ToString().ShouldBe("CAMPAIGN_RECIPIENTS_CHANGED");
        problem["expectedRecipientCount"].ToString().ShouldBe((count + 13).ToString());
        problem["actualRecipientCount"].ToString().ShouldBe(count.ToString());

        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
    }

    /// <summary>
    /// El campo es obligatorio: omitirlo es un 400 y no un lanzamiento que se salta la
    /// comprobación. Un campo opcional acá fallaría abierto justo en el cliente que más necesita
    /// la guarda, uno escrito deprisa contra Scalar.
    /// </summary>
    [Fact]
    public async Task Launch_Returns400AndLaunchesNothing_WhenTheCountIsOmitted()
    {
        var client = _factory.CreateClient();
        var (campaign, _) = await CampaignWithRecipientsAsync(client);

        var response = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
    }

    [Fact]
    public async Task Launch_Returns400_WhenTheCountIsNegative()
    {
        var client = _factory.CreateClient();
        var (campaign, _) = await CampaignWithRecipientsAsync(client);

        var response = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = -1 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
    }

    #endregion

    /// <summary>
    /// Un borrador cuya pareja no tiene plantilla —guardado antes de que crear y editar lo
    /// rechazaran, o cuya variante desapareció— se rechaza antes del <c>202</c>. Antes contestaba
    /// <c>202</c> y el worker lo dejaba atascado en <c>sending</c> con
    /// <c>CAMPAIGN_NOT_RENDERABLE</c> (spec <c>03</c> §4.1). Va en el paso 1, así que al CRM no se
    /// le pregunta nada.
    /// </summary>
    [Fact]
    public async Task Launch_ReturnsBadRequestWithTemplateNotFound_WhenTheCampaignLanguageHasNoTemplate()
    {
        var client = _factory.CreateClient();
        var (campaign, count) = await CampaignWithRecipientsAsync(client);

        await CampaignTestClient.SetCampaignLanguageAsync(_factory, campaign.Id, "en");
        var mailabilityCallsBefore = _factory.Crm.MailabilityCallCount;

        var response = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = count });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem!["code"].ToString().ShouldBe("CAMPAIGN_TEMPLATE_NOT_FOUND");
        problem["templateKey"].ToString().ShouldBe(CampaignTestClient.TemplateKey);
        problem["language"].ToString().ShouldBe("en");

        _factory.Crm.MailabilityCallCount.ShouldBe(mailabilityCallsBefore);
        await ShouldStillBeAnUnlaunchedDraftAsync(client, campaign.Id);
        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);
    }

    [Fact]
    public async Task Launch_Returns404_WhenTheCampaignIsUnknown()
    {
        var response = await CampaignTestClient.LaunchAsync(
            _factory.CreateClient(),
            Guid.NewGuid(),
            new { expectedRecipientCount = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Un borrador con destinatarios contactables, y cuántos son. Convoca el evento que sirve
    /// <see cref="EventsStub"/>: el lanzamiento lo lee en su paso 4 y un id que events-service no
    /// conoce no tiene ni nombre, ni lugar, ni fecha que poner en el correo.
    /// </summary>
    private static async Task<(CampaignResponse Campaign, int Count)> CampaignWithRecipientsAsync(HttpClient client)
    {
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withRecipients = await CampaignTestClient.GetAsync(client, campaign.Id);

        withRecipients.RecipientCount.ShouldBeGreaterThan(0);

        return (withRecipients, withRecipients.RecipientCount);
    }

    private static async Task ShouldStillBeAnUnlaunchedDraftAsync(HttpClient client, Guid id)
    {
        var campaign = await CampaignTestClient.GetAsync(client, id);

        campaign.State.ShouldBe(CampaignState.Draft);
        campaign.LaunchedById.ShouldBeNull();
        campaign.LaunchedAt.ShouldBeNull();
        campaign.SentAt.ShouldBeNull();
    }

    private static async Task ShouldCarryCodeAsync(HttpResponseMessage response, string code)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem.ShouldNotBeNull();
        problem["code"].ToString().ShouldBe(code);
    }
}
