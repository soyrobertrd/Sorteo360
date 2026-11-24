using System.Net;
using System.Net.Http.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// Armar, leer, editar y cancelar una campaña, contra un PostgreSQL real.
/// <para>
/// Lo que más se cobra acá es la guarda de borrador: una campaña que ya salió es de solo
/// lectura, y es la guarda entera en la que se apoyan la selección y las cargas de la spec
/// <c>05</c>.
/// </para>
/// </summary>
public sealed class CampaignEndpointsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    /// <summary>
    /// Un idioma admitido que ningún test de esta clase siembra. Los que siembran usan <c>en</c> y
    /// <c>fr</c>, y comparten la base con estos.
    /// </summary>
    private const string UnseededLanguage = "pt";

    private readonly ConvocatoriasApiDbFactory _factory;

    public CampaignEndpointsTests(ConvocatoriasApiDbFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
    }

    #region Crear y leer

    [Fact]
    public async Task Create_ReturnsADraftWithNoRecipientsAndNoLaunchStamp_WhenThePayloadIsValid()
    {
        var client = _factory.CreateClient();
        var eventId = Guid.NewGuid();

        var campaign = await CampaignTestClient.CreateAsync(client, eventId);

        campaign.EventId.ShouldBe(eventId);
        campaign.State.ShouldBe(CampaignState.Draft);
        campaign.RecipientCount.ShouldBe(0);
        campaign.ExclusionCount.ShouldBe(0);
        campaign.LaunchedById.ShouldBeNull();
        campaign.LaunchedAt.ShouldBeNull();
        campaign.SentAt.ShouldBeNull();
    }

    [Fact]
    public async Task Create_Returns201WithALocationThatResolves_WhenThePayloadIsValid()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(CampaignTestClient.Route, new
        {
            eventId = Guid.NewGuid(),
            templateKey = CampaignTestClient.TemplateKey,
            language = "es",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var followed = await client.GetAsync(response.Headers.Location!.ToString().TrimStart('/'));

        followed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Returns404_WhenTheIdIsUnknown()
    {
        var response = await _factory.CreateClient().GetAsync($"{CampaignTestClient.Route}/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("")]
    public async Task Create_Returns400_WhenTheLanguageIsOutsideTheCatalogue(string language)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(CampaignTestClient.Route, new
        {
            eventId = Guid.NewGuid(),
            templateKey = CampaignTestClient.TemplateKey,
            language,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region El listado

    /// <summary>
    /// Solo el español está sembrado. Un idioma admitido sin plantilla no se guarda: se guardaba,
    /// se lanzaba con <c>202</c> y el worker lo dejaba atascado en <c>sending</c> (spec <c>03</c>
    /// §4.1). El rechazo nombra la pareja que falta y no deja ninguna campaña detrás.
    /// </summary>
    [Fact]
    public async Task Create_ReturnsBadRequestWithTemplateNotFound_WhenTheLanguageHasNoTemplate()
    {
        var client = _factory.CreateClient();
        var eventId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(CampaignTestClient.Route, new
        {
            eventId,
            templateKey = CampaignTestClient.TemplateKey,
            language = UnseededLanguage,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldNameTheMissingTemplateAsync(response, CampaignTestClient.TemplateKey, UnseededLanguage);

        (await ListAsync(client, $"?eventId={eventId}")).Total.ShouldBe(0);
    }

    [Fact]
    public async Task Create_ReturnsBadRequestWithTemplateNotFound_WhenTheTemplateKeyDoesNotExist()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(CampaignTestClient.Route, new
        {
            eventId = Guid.NewGuid(),
            templateKey = "no_such_template",
            language = "es",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldNameTheMissingTemplateAsync(response, "no_such_template", "es");
    }

    [Fact]
    public async Task List_ReturnsOnlyThatEventsCampaigns_WhenFilteredByEvent()
    {
        var client = _factory.CreateClient();
        var eventId = Guid.NewGuid();

        await CampaignTestClient.CreateAsync(client, eventId);
        await CampaignTestClient.CreateAsync(client, eventId);
        await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var page = await ListAsync(client, $"?eventId={eventId}");

        page.Total.ShouldBe(2);
        page.Items.ShouldAllBe(campaign => campaign.EventId == eventId);
    }

    [Fact]
    public async Task List_ReturnsOnlyCampaignsInThatState_WhenFilteredByState()
    {
        var client = _factory.CreateClient();
        var eventId = Guid.NewGuid();

        var cancelled = await CampaignTestClient.CreateAsync(client, eventId);
        await CampaignTestClient.CreateAsync(client, eventId);

        (await CampaignTestClient.CancelAsync(client, cancelled.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await ListAsync(client, $"?eventId={eventId}&state={CampaignState.Cancelled}");

        page.Total.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(cancelled.Id);
    }

    /// <summary>
    /// Un estado con errata devuelve nada y no todo. Colar el filtro que no se entiende es la
    /// forma de que alguien crea que no hay campañas en ese estado cuando lo que hay es una
    /// errata.
    /// </summary>
    [Fact]
    public async Task List_ReturnsAnEmptyPage_WhenTheStateFilterIsOutsideTheCatalogue()
    {
        var client = _factory.CreateClient();
        var eventId = Guid.NewGuid();

        await CampaignTestClient.CreateAsync(client, eventId);

        var page = await ListAsync(client, $"?eventId={eventId}&state=sendinng");

        page.Total.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_ReturnsAPageSizeCappedAt100_WhenALargerOneIsAsked()
    {
        var page = await ListAsync(_factory.CreateClient(), "?pageSize=5000");

        page.PageSize.ShouldBe(100);
    }

    #endregion

    #region La guarda de borrador

    [Fact]
    public async Task Update_Returns200WithTheNewTemplateAndLanguage_WhenTheCampaignIsADraft()
    {
        var client = _factory.CreateClient();
        await CampaignTestClient.SeedCampaignTemplateAsync(_factory, "save_the_date", "en");
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.PatchAsJsonAsync($"{CampaignTestClient.Route}/{campaign.Id}", new
        {
            templateKey = "save_the_date",
            language = "en",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<CampaignResponse>();

        updated!.TemplateKey.ShouldBe("save_the_date");
        updated.Language.ShouldBe("en");
    }

    /// <summary>
    /// Es un <c>PATCH</c>: mandar solo el idioma no borra la plantilla. Con los dos campos
    /// obligatorios esto sería un 400, y una pantalla que se olvidara de reenviar la plantilla la
    /// perdería.
    /// </summary>
    [Fact]
    public async Task Update_ReturnsTheOtherFieldUnchanged_WhenOnlyOneFieldIsSent()
    {
        var client = _factory.CreateClient();
        await CampaignTestClient.SeedCampaignTemplateAsync(_factory, "save_the_date", "fr");
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid(), "save_the_date", "es");

        var response = await client.PatchAsJsonAsync($"{CampaignTestClient.Route}/{campaign.Id}", new
        {
            language = "fr",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<CampaignResponse>();

        updated!.Language.ShouldBe("fr");
        updated.TemplateKey.ShouldBe("save_the_date");
    }

    /// <summary>
    /// Cambiar solo el idioma también cuenta: la pareja se comprueba ya resuelta, con la plantilla
    /// que la campaña ya tenía. El rechazo no toca la fila.
    /// </summary>
    [Fact]
    public async Task Update_ReturnsBadRequestWithTemplateNotFound_WhenOnlyTheLanguageChangesToOneWithNoTemplate()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.PatchAsJsonAsync($"{CampaignTestClient.Route}/{campaign.Id}", new
        {
            language = UnseededLanguage,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldNameTheMissingTemplateAsync(response, CampaignTestClient.TemplateKey, UnseededLanguage);

        var unchanged = await CampaignTestClient.GetAsync(client, campaign.Id);

        unchanged.TemplateKey.ShouldBe(CampaignTestClient.TemplateKey);
        unchanged.Language.ShouldBe("es");
    }

    [Fact]
    public async Task Update_ReturnsBadRequestWithTemplateNotFound_WhenOnlyTheTemplateKeyChangesToOneMissingInTheLanguage()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.PatchAsJsonAsync($"{CampaignTestClient.Route}/{campaign.Id}", new
        {
            templateKey = "no_such_template",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldNameTheMissingTemplateAsync(response, "no_such_template", "es");
        (await CampaignTestClient.GetAsync(client, campaign.Id)).TemplateKey.ShouldBe(CampaignTestClient.TemplateKey);
    }

    /// <summary>
    /// Presente y vacío no es "no lo toques" sino "déjalo en blanco", y una campaña sin plantilla
    /// no se puede mandar.
    /// </summary>
    [Fact]
    public async Task Update_Returns400_WhenAFieldIsBlanked()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.PatchAsJsonAsync($"{CampaignTestClient.Route}/{campaign.Id}", new
        {
            templateKey = "",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Returns409CampaignNotDraft_WhenTheCampaignIsSending()
    {
        var client = _factory.CreateClient();
        var campaign = await LaunchedCampaignAsync(client);

        var response = await client.PatchAsJsonAsync($"{CampaignTestClient.Route}/{campaign.Id}", new
        {
            templateKey = "save_the_date",
            language = "es",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldCarryCodeAsync(response, "CAMPAIGN_NOT_DRAFT");
    }

    [Fact]
    public async Task RecipientChange_Returns409CampaignNotDraft_WhenTheCampaignIsSending()
    {
        var client = _factory.CreateClient();
        var campaign = await LaunchedCampaignAsync(client);
        var recipientId = (await CampaignTestClient.RecipientIdsAsync(_factory, campaign.Id)).First();

        var removal = await client.DeleteAsync($"{CampaignTestClient.Route}/{campaign.Id}/recipients/{recipientId}");
        var selection = await CampaignTestClient.SelectAsync(client, campaign.Id);

        removal.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        selection.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldCarryCodeAsync(removal, "CAMPAIGN_NOT_DRAFT");
        await ShouldCarryCodeAsync(selection, "CAMPAIGN_NOT_DRAFT");
    }

    [Fact]
    public async Task RemoveRecipient_Returns204AndDecrementsTheCount_WhenTheCampaignIsADraft()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var recipientId = (await CampaignTestClient.RecipientIdsAsync(_factory, campaign.Id)).Single();

        var response = await client.DeleteAsync($"{CampaignTestClient.Route}/{campaign.Id}/recipients/{recipientId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CampaignTestClient.GetAsync(client, campaign.Id)).RecipientCount.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveRecipient_Returns404_WhenTheRecipientIsNotThere()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.DeleteAsync(
            $"{CampaignTestClient.Route}/{campaign.Id}/recipients/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Cancelar

    [Fact]
    public async Task Cancel_Returns200AndACancelledCampaign_WhenTheCampaignIsADraft()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await CampaignTestClient.CancelAsync(client, campaign.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CampaignTestClient.GetAsync(client, campaign.Id)).State.ShouldBe(CampaignState.Cancelled);
    }

    /// <summary>
    /// Cancelar en pleno envío deja intactas las filas sin tocar. Una <c>pending</c> bajo una
    /// campaña cancelada se lee como "pretendíamos escribirle y paramos", y reescribirlas
    /// borraría esa lectura.
    /// </summary>
    [Fact]
    public async Task Cancel_Returns200AndLeavesUntouchedRowsPending_WhenTheCampaignIsSending()
    {
        var client = _factory.CreateClient();
        var campaign = await LaunchedCampaignAsync(client);

        var response = await CampaignTestClient.CancelAsync(client, campaign.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CampaignTestClient.GetAsync(client, campaign.Id)).State.ShouldBe(CampaignState.Cancelled);
        (await CampaignTestClient.RecipientStatusesAsync(_factory, campaign.Id))
            .ShouldAllBe(status => status == RecipientSendStatus.Pending);
    }

    [Fact]
    public async Task Cancel_Returns409TransitionNotAllowed_WhenTheCampaignIsAlreadyCancelled()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        (await CampaignTestClient.CancelAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await CampaignTestClient.CancelAsync(client, campaign.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldCarryCodeAsync(response, "CAMPAIGN_TRANSITION_NOT_ALLOWED");
    }

    [Fact]
    public async Task Cancel_Returns404_WhenTheCampaignIsUnknown()
    {
        var response = await CampaignTestClient.CancelAsync(_factory.CreateClient(), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Autorización

    [Fact]
    public async Task CampaignRoutes_Returns401_WhenThereIsNoToken()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, CampaignTestClient.Route);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Quien arma no manda. Es la única prueba de que la división en dos roles es real y no un
    /// par de constantes que apuntan al mismo sitio.
    /// </summary>
    [Fact]
    public async Task CampaignRoutes_Returns403OnLaunchAnd200OnEdit_WhenTheCallerIsAWriterWithoutLaunchRole()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var writer = _factory.CreateClient();
        writer.DefaultRequestHeaders.Add(
            TestAuthHandler.RolesHeader,
            $"{AuthorizationPolicies.CampaignsWrite},{AuthorizationPolicies.ConvocatoriasRead}");

        var edit = await writer.PatchAsJsonAsync($"{CampaignTestClient.Route}/{campaign.Id}", new
        {
            templateKey = "save_the_date",
            language = "es",
        });

        var launch = await CampaignTestClient.LaunchAsync(writer, campaign.Id, new { expectedRecipientCount = 1 });

        edit.StatusCode.ShouldBe(HttpStatusCode.OK);
        launch.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Returns403_WhenTheCallerLacksTheWriteRole()
    {
        var reader = _factory.CreateClient();
        reader.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.ConvocatoriasRead);

        var response = await reader.PostAsJsonAsync(CampaignTestClient.Route, new
        {
            eventId = Guid.NewGuid(),
            templateKey = CampaignTestClient.TemplateKey,
            language = "es",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    private async Task<PagedResponse<CampaignResponse>> ListAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{CampaignTestClient.Route}{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<PagedResponse<CampaignResponse>>()
            ?? throw new InvalidOperationException("El listado llegó vacío.");
    }

    /// <summary>Un borrador con un destinatario contactable, ya lanzado y por tanto en <c>sending</c>.</summary>
    private async Task<CampaignResponse> LaunchedCampaignAsync(HttpClient client)
    {
        // Convoca el evento que sirve el vecino simulado: el lanzamiento lo lee en su paso 4 y
        // un id que events-service no conoce no llega a `sending` (spec 04 §1).
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var count = (await CampaignTestClient.GetAsync(client, campaign.Id)).RecipientCount;
        var launch = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = count });

        launch.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        return await CampaignTestClient.GetAsync(client, campaign.Id);
    }

    private static async Task ShouldNameTheMissingTemplateAsync(
        HttpResponseMessage response,
        string templateKey,
        string language)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem.ShouldNotBeNull();
        problem["code"].ToString().ShouldBe("CAMPAIGN_TEMPLATE_NOT_FOUND");
        problem["templateKey"].ToString().ShouldBe(templateKey);
        problem["language"].ToString().ShouldBe(language);
    }

    private static async Task ShouldCarryCodeAsync(HttpResponseMessage response, string code)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem.ShouldNotBeNull();
        problem["code"].ToString().ShouldBe(code);
    }
}
