using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.ContactCampaignEndpoints;

/// <summary>
/// Las campañas enviadas a un contacto del CRM (PGTI-176). Cada test siembra sus campañas por SQL
/// con contactos y eventos recién inventados, así que ninguno ve las filas de otro.
/// </summary>
public sealed class ContactCampaignEndpointsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private const string Route = "/v1/convocatorias/campaign-recipients";

    private readonly ConvocatoriasApiDbFactory _factory;

    public ContactCampaignEndpointsTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task ListContactCampaigns_Returns403_WhenTheCallerLacksConvocatoriasRead()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsWrite);

        var response = await client.GetAsync($"{Route}?contactId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListContactCampaigns_Returns400_WhenNoContactIdIsSent()
    {
        var response = await _factory.CreateClient().GetAsync(Route);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListContactCampaigns_Returns400_WhenMoreThanAHundredContactIdsAreSent()
    {
        var query = string.Join('&', Enumerable.Range(0, 101).Select(_ => $"contactId={Guid.NewGuid()}"));

        var response = await _factory.CreateClient().GetAsync($"{Route}?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>La fila entera: la campaña, su evento, la fecha del lanzamiento y el estado del envío.</summary>
    [Fact]
    public async Task ListContactCampaigns_ReturnsTheLaunchDateAndSendStatus_WhenAnEventCampaignReachedTheContact()
    {
        var contact = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var launchedAt = new DateTimeOffset(2026, 3, 12, 14, 0, 0, TimeSpan.Zero);
        var campaign = await SeedCampaignAsync(eventId, launchedAt);
        await SeedRecipientAsync(campaign, contact, "ana@acme.com", RecipientSendStatus.Failed);

        var row = (await ListAsync(contact)).ShouldHaveSingleItem();

        row.GetProperty("contactId").GetGuid().ShouldBe(contact);
        row.GetProperty("campaignId").GetGuid().ShouldBe(campaign);
        row.GetProperty("eventId").GetGuid().ShouldBe(eventId);
        row.GetProperty("subject").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("launchedAt").GetDateTimeOffset().ShouldBe(launchedAt);
        row.GetProperty("sendStatus").GetString().ShouldBe(RecipientSendStatus.Failed);
        row.GetProperty("registered").GetBoolean().ShouldBeFalse();
    }

    /// <summary>
    /// Inscrito es la misma unión que la lista de participantes (spec 07 §3.1): una inscripción en el
    /// evento con la dirección a la que se le escribió, sin importar mayúsculas ni espacios.
    /// </summary>
    [Fact]
    public async Task ListContactCampaigns_ReturnsRegisteredTrue_WhenTheContactRegisteredWithTheInvitedAddress()
    {
        var contact = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(eventId, DateTimeOffset.UtcNow.AddDays(-2));
        var email = $"{Guid.NewGuid():N}@acme.com";
        await SeedRecipientAsync(campaign, contact, $" {email.ToUpperInvariant()} ");
        await SeedRegistrationAsync(eventId, email);

        var row = (await ListAsync(contact)).ShouldHaveSingleItem();

        row.GetProperty("registered").GetBoolean().ShouldBeTrue();
    }

    /// <summary>Una inscripción con esa dirección en otro evento no es haberse inscrito en este.</summary>
    [Fact]
    public async Task ListContactCampaigns_ReturnsRegisteredFalse_WhenTheAddressRegisteredOnlyForAnotherEvent()
    {
        var contact = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2));
        var email = $"{Guid.NewGuid():N}@acme.com";
        await SeedRecipientAsync(campaign, contact, email);
        await SeedRegistrationAsync(Guid.NewGuid(), email);

        var row = (await ListAsync(contact)).ShouldHaveSingleItem();

        row.GetProperty("registered").GetBoolean().ShouldBeFalse();
    }

    /// <summary>Un mensaje directo (PGTI-174) no tiene evento ni en qué inscribirse: trae su asunto.</summary>
    [Fact]
    public async Task ListContactCampaigns_ReturnsSubjectAndNullRegistered_WhenTheCampaignHasNoEvent()
    {
        var contact = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(eventId: null, DateTimeOffset.UtcNow.AddDays(-1));
        await SeedRecipientAsync(campaign, contact, "ana@acme.com");

        var row = (await ListAsync(contact)).ShouldHaveSingleItem();

        row.GetProperty("eventId").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("subject").GetString().ShouldBe("Novedades");
        row.GetProperty("registered").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>A nadie se le escribió desde un borrador, aunque ya tenga su lista seleccionada.</summary>
    [Fact]
    public async Task ListContactCampaigns_ReturnsNothing_WhenTheContactIsOnlyOnADraft()
    {
        var contact = Guid.NewGuid();
        var draft = await SeedCampaignAsync(Guid.NewGuid(), launchedAt: null);
        await SeedRecipientAsync(draft, contact, "ana@acme.com", RecipientSendStatus.Pending);

        var rows = await ListAsync(contact);

        rows.ShouldBeEmpty();
    }

    /// <summary>
    /// La empresa pregunta por todos sus contactos de una vez. Cada fila dice de quién es, y un
    /// contacto que no se pidió no aparece aunque estuviera en la misma campaña.
    /// </summary>
    [Fact]
    public async Task ListContactCampaigns_ReturnsTheRowsOfEveryAskedContactNewestFirst_WhenSeveralContactIdsAreSent()
    {
        var ana = Guid.NewGuid();
        var luis = Guid.NewGuid();
        var january = await SeedCampaignAsync(Guid.NewGuid(), new DateTimeOffset(2026, 1, 22, 10, 0, 0, TimeSpan.Zero));
        var may = await SeedCampaignAsync(Guid.NewGuid(), new DateTimeOffset(2026, 5, 6, 15, 0, 0, TimeSpan.Zero));
        await SeedRecipientAsync(january, ana, "ana@acme.com");
        await SeedRecipientAsync(january, Guid.NewGuid(), "otro@acme.com");
        await SeedRecipientAsync(may, ana, "ana@acme.com");
        await SeedRecipientAsync(may, luis, "luis@acme.com");

        var rows = await ListAsync(ana, luis);

        rows.Select(r => r.GetProperty("campaignId").GetGuid()).ShouldBe([may, may, january]);
        rows.Select(r => r.GetProperty("contactId").GetGuid()).ShouldBe([ana, luis, ana], ignoreOrder: true);
    }

    private async Task<JsonElement[]> ListAsync(params Guid[] contactIds)
    {
        var query = string.Join('&', contactIds.Select(id => $"contactId={id}"));
        var response = await _factory.CreateClient().GetAsync($"{Route}?{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return [.. JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.EnumerateArray()];
    }

    /// <summary>
    /// Una campaña de evento, o un mensaje directo sin él; lanzada si trae fecha, borrador si no.
    /// </summary>
    private async Task<Guid> SeedCampaignAsync(Guid? eventId, DateTimeOffset? launchedAt)
    {
        var id = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();
        Guid? launcher = launchedAt is null ? null : actor;
        var state = launchedAt is null ? CampaignState.Draft : CampaignState.Sent;
        var template = eventId is null ? "direct_message" : "registration_invite";
        var subject = eventId is null ? "Novedades" : null;
        var message = eventId is null ? "Hola." : null;
        await ExecuteAsync($"""
            INSERT INTO convocatorias.campaign
                (id, event_id, template_key, language, state, subject, message, launched_by_id, launched_at,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES ({id}, {eventId}, {template}, 'es', {state}, {subject}, {message}, {launcher}, {launchedAt},
                    now(), now(), {actor}, {actor})
            """);
        return id;
    }

    private Task SeedRecipientAsync(
        Guid campaignId,
        Guid contactId,
        string email,
        string sendStatus = RecipientSendStatus.HandedOff) =>
        ExecuteAsync($"""
            INSERT INTO convocatorias.campaign_recipient
                (id, campaign_id, contact_id, first_name, last_name, email, send_status, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, {campaignId}, {contactId}, 'Ana', 'Gómez', {email}, {sendStatus}, now(), now())
            """);

    private Task SeedRegistrationAsync(Guid eventId, string email)
    {
        var unique = Guid.NewGuid().ToString("N");
        return ExecuteAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, first_name, last_name, email, email_normalized, reference, qr_code,
                 contact_details_differ, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, {eventId}, 'Ana', 'Gómez', {email}, {email.Trim().ToLowerInvariant()},
                    {"MITUR-2026-" + unique}, {unique}, false, now(), now())
            """);
    }

    private async Task ExecuteAsync(FormattableString sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync(sql);
    }
}
