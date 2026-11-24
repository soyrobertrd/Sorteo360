using System.Net;
using System.Net.Http.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// La campaña sin evento (PGTI-174): un mensaje directo con su asunto y su texto, armado y editado
/// por las mismas rutas que una campaña de evento, contra un PostgreSQL real.
/// </summary>
public sealed class CampaignDirectMessageTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public CampaignDirectMessageTests(ConvocatoriasApiDbFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
    }

    [Fact]
    public async Task Create_ReturnsADraftWithNoEventAndItsText_WhenTheTemplateIsDirectMessage()
    {
        var campaign = await CampaignTestClient.CreateDirectMessageAsync(
            _factory.CreateClient(),
            subject: "Novedades",
            message: "Hola.\nHasta pronto.");

        campaign.EventId.ShouldBeNull();
        campaign.TemplateKey.ShouldBe(TemplateKey.DirectMessage);
        campaign.Subject.ShouldBe("Novedades");
        campaign.Message.ShouldBe("Hola.\nHasta pronto.");
        campaign.State.ShouldBe(CampaignState.Draft);
    }

    [Fact]
    public async Task Create_ReturnsNoSubjectOrMessage_WhenTheCampaignHasAnEvent()
    {
        var campaign = await CampaignTestClient.CreateAsync(_factory.CreateClient(), Guid.NewGuid());

        campaign.Subject.ShouldBeNull();
        campaign.Message.ShouldBeNull();
    }

    public static TheoryData<string> Mismatches() => new()
    {
        // Sin evento con una plantilla de evento: nada con qué rellenar {{evento}}.
        """{ "templateKey": "registration_invite", "language": "es" }""",
        // Un mensaje directo con evento.
        $$"""{ "eventId": "{{Guid.NewGuid()}}", "templateKey": "direct_message", "language": "es", "subject": "A", "message": "B" }""",
        // Un mensaje directo sin texto.
        """{ "templateKey": "direct_message", "language": "es", "subject": "A" }""",
        // Una campaña de evento con texto propio.
        $$"""{ "eventId": "{{Guid.NewGuid()}}", "templateKey": "registration_invite", "language": "es", "message": "B" }""",
    };

    [Theory]
    [MemberData(nameof(Mismatches))]
    public async Task Create_Returns400DirectMessageMismatch_WhenTheEventAndTheTemplateDisagree(string body)
    {
        var response = await _factory.CreateClient().PostAsync(
            CampaignTestClient.Route,
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldCarryCodeAsync(response, CampaignDirectMessageMismatchError.ErrorCode);
    }

    [Fact]
    public async Task Create_Returns400_WhenTheSubjectIsTooLong()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(CampaignTestClient.Route, new
        {
            templateKey = TemplateKey.DirectMessage,
            language = "es",
            subject = new string('a', CampaignDirectMessage.SubjectMaxLength + 1),
            message = "Texto",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Returns200WithTheNewText_WhenADirectMessageIsEdited()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateDirectMessageAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"{CampaignTestClient.Route}/{campaign.Id}",
            new { message = "Texto nuevo" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await CampaignTestClient.GetAsync(client, campaign.Id);
        updated.Message.ShouldBe("Texto nuevo");
        updated.Subject.ShouldBe(campaign.Subject);
    }

    [Fact]
    public async Task Update_Returns400DirectMessageMismatch_WhenAnEventCampaignGetsAMessage()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.PatchAsJsonAsync(
            $"{CampaignTestClient.Route}/{campaign.Id}",
            new { message = "Texto" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldCarryCodeAsync(response, CampaignDirectMessageMismatchError.ErrorCode);

        (await CampaignTestClient.GetAsync(client, campaign.Id)).Message.ShouldBeNull();
    }

    [Fact]
    public async Task Update_Returns400DirectMessageMismatch_WhenADirectMessageTakesAnEventTemplate()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateDirectMessageAsync(client);

        var response = await client.PatchAsJsonAsync(
            $"{CampaignTestClient.Route}/{campaign.Id}",
            new { templateKey = TemplateKey.RegistrationInvite });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldCarryCodeAsync(response, CampaignDirectMessageMismatchError.ErrorCode);
    }

    /// <summary>
    /// Un mensaje directo se lanza sin evento: pasa por el CRM como cualquier campaña y no tiene
    /// evento que leer. Lo que sale después está en <c>StubCampaignSendGatewayTests</c>.
    /// </summary>
    [Fact]
    public async Task Launch_Returns202WithoutAnEvent_WhenTheCampaignIsADirectMessage()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateDirectMessageAsync(client);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var launched = await CampaignTestClient.LaunchReadyAsync(client, campaign.Id);

        (await CampaignTestClient.GetAsync(client, launched.Id)).State.ShouldBe(CampaignState.Sending);
    }

    /// <summary>
    /// Las comprobaciones de consentimiento son las de cualquier campaña: la selección excluye a
    /// quien se dio de baja, y la recomprobación del lanzamiento suprime a quien el CRM ya no deja
    /// escribirle, hasta negar el lanzamiento si no queda nadie.
    /// </summary>
    [Fact]
    public async Task Launch_Returns400NoMailableRecipients_WhenTheCrmSuppressesEveryDirectMessageRecipient()
    {
        var ana = new StubContact(Guid.NewGuid(), "Ana", "Pérez", "ana.dm@acme.com", CanBeEmailed: true, SuppressionReason: null);
        var luis = new StubContact(Guid.NewGuid(), "Luis", "Gómez", "luis.dm@acme.com", CanBeEmailed: false, "opted_out");

        _factory.Crm.ServeContacts(ana, luis);

        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateDirectMessageAsync(client);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var selected = await CampaignTestClient.GetAsync(client, campaign.Id);
        selected.RecipientCount.ShouldBe(1);
        selected.ExclusionCount.ShouldBe(1);

        _factory.Crm.ServeMailability(ana with { CanBeEmailed = false, SuppressionReason = "do_not_invite" });

        var launch = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = 1 });

        launch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldCarryCodeAsync(launch, "CAMPAIGN_HAS_NO_MAILABLE_RECIPIENTS");
    }

    private static async Task ShouldCarryCodeAsync(HttpResponseMessage response, string code)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        problem.ShouldNotBeNull();
        problem["code"].ToString().ShouldBe(code);
    }
}
