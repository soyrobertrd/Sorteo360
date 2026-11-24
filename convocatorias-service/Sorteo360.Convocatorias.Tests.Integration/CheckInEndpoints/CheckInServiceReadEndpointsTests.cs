using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CheckInEndpoints;

/// <summary>
/// La lista de check-ins que lee la rifa (spec <c>09</c> §7, PGTI-130): quién entró al evento, con su
/// procedencia y su contacto del CRM. Cada test siembra su evento por SQL, como la lista de
/// participantes, porque la procedencia depende de campañas lanzadas.
/// </summary>
public sealed class CheckInServiceReadEndpointsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private static readonly DateTimeOffset DoorOpens = new(2026, 10, 3, 18, 0, 0, TimeSpan.Zero);

    private readonly ConvocatoriasApiDbFactory _factory;

    public CheckInServiceReadEndpointsTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task List_ReturnsEachOrigin_WhenAnInviteeAReferralAndAWalkInCheckedIn()
    {
        var eventId = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(eventId, launched: true);
        var ana = await SeedRecipientAsync(campaign, "ana@acme.com");
        await SeedRegistrationAsync(eventId, "ana@acme.com", checkedInAt: DoorOpens, campaignRecipientId: ana);
        await SeedRegistrationAsync(eventId, "colega@acme.com", checkedInAt: DoorOpens.AddMinutes(1), campaignRecipientId: ana);
        await SeedRegistrationAsync(eventId, "libre@acme.com", checkedInAt: DoorOpens.AddMinutes(2));

        var items = await ItemsAsync(eventId);

        items.Select(Origin).ShouldBe(["invited", "referred", "walk_in"]);
        items.Select(EmailNormalized).ShouldBe(["ana@acme.com", "colega@acme.com", "libre@acme.com"]);
    }

    /// <summary>
    /// La procedencia no mira el envío (decisión 3 de PGTI-130): una destinataria suprimida o fallida
    /// que se inscribió en esa misma dirección, sin enlace, sigue siendo <c>invited</c>.
    /// </summary>
    [Theory]
    [InlineData(RecipientSendStatus.Suppressed)]
    [InlineData(RecipientSendStatus.Failed)]
    public async Task List_ReturnsInvited_WhenTheRecipientWasNotHandedOffAndRegisteredWithoutALink(string sendStatus)
    {
        var eventId = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(eventId, launched: true);
        await SeedRecipientAsync(campaign, "ana@acme.com", sendStatus);
        await SeedRegistrationAsync(eventId, "  ANA@acme.com ", checkedInAt: DoorOpens);

        var item = (await ItemsAsync(eventId)).ShouldHaveSingleItem();

        Origin(item).ShouldBe("invited");
    }

    /// <summary>
    /// El contacto es el de quien se inscribió, nunca el de la colega que le reenvió el correo
    /// (§7.1): correr la lista negra contra ella dejaría pasar a la persona equivocada.
    /// </summary>
    [Fact]
    public async Task List_ReturnsTheRegistrantsOwnContact_WhenTheyWereReferred()
    {
        var eventId = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(eventId, launched: true);
        var forwarderContact = Guid.CreateVersion7();
        var registrantContact = Guid.CreateVersion7();
        var ana = await SeedRecipientAsync(campaign, "ana@acme.com", contactId: forwarderContact);
        await SeedRegistrationAsync(
            eventId, "colega@acme.com", checkedInAt: DoorOpens, campaignRecipientId: ana, crmContactId: registrantContact);

        var item = (await ItemsAsync(eventId)).ShouldHaveSingleItem();

        Origin(item).ShouldBe("referred");
        item.GetProperty("contactId").GetGuid().ShouldBe(registrantContact);
    }

    [Fact]
    public async Task List_ReturnsANullContact_WhenTheRegistrationsCrmWriteIsPending()
    {
        var eventId = Guid.NewGuid();
        await SeedRegistrationAsync(eventId, "libre@acme.com", checkedInAt: DoorOpens);

        var item = (await ItemsAsync(eventId)).ShouldHaveSingleItem();

        item.GetProperty("contactId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task List_LeavesOutWhoNeverCheckedInAndAnUndoneCheckIn_WhenRead()
    {
        var eventId = Guid.NewGuid();
        var present = await SeedRegistrationAsync(eventId, "presente@acme.com", checkedInAt: DoorOpens);
        await SeedRegistrationAsync(eventId, "ausente@acme.com");
        var undone = await SeedRegistrationAsync(eventId, "deshecho@acme.com");
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync(
            $"/v1/convocatorias/events/{eventId}/check-ins/by-registration", new { registrationId = undone }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.DeleteAsync($"/v1/convocatorias/events/{eventId}/check-ins/{undone}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var item = (await ItemsAsync(eventId)).ShouldHaveSingleItem();

        item.GetProperty("registrationId").GetGuid().ShouldBe(present);
    }

    /// <summary>
    /// Un borrador no invitó a nadie: quien está en su lista y se inscribió sin enlace llegó por la
    /// cartelera (<c>LaunchedCampaign.Is</c>, spec <c>07</c> §2).
    /// </summary>
    [Fact]
    public async Task List_ReturnsWalkIn_WhenTheOnlyCampaignListingTheAddressIsADraft()
    {
        var eventId = Guid.NewGuid();
        var draft = await SeedCampaignAsync(eventId, launched: false);
        await SeedRecipientAsync(draft, "ana@acme.com", RecipientSendStatus.Pending);
        await SeedRegistrationAsync(eventId, "ana@acme.com", checkedInAt: DoorOpens);

        var item = (await ItemsAsync(eventId)).ShouldHaveSingleItem();

        Origin(item).ShouldBe("walk_in");
    }

    [Fact]
    public async Task List_ReturnsCheckedInAtThenRegistrationIdOrder_WhenTwoShareTheStamp()
    {
        var eventId = Guid.NewGuid();
        var late = await SeedRegistrationAsync(eventId, "tarde@acme.com", checkedInAt: DoorOpens.AddMinutes(5));
        var first = await SeedRegistrationAsync(eventId, "primera@acme.com", checkedInAt: DoorOpens);
        var second = await SeedRegistrationAsync(eventId, "segunda@acme.com", checkedInAt: DoorOpens);
        Guid[] tied = [.. new[] { first, second }.OrderBy(id => id.ToString(), StringComparer.Ordinal)];

        var items = await ItemsAsync(eventId);

        items.Select(item => item.GetProperty("registrationId").GetGuid()).ShouldBe([tied[0], tied[1], late]);
    }

    [Fact]
    public async Task List_ReturnsExactlyTheSpecFieldsInsideItems_WhenRead()
    {
        var eventId = Guid.NewGuid();
        var contact = Guid.CreateVersion7();
        var registration = await SeedRegistrationAsync(eventId, "  Maria@Acme.com", checkedInAt: DoorOpens, crmContactId: contact);

        var body = await ReadJsonAsync(await ListAsync(eventId));

        body.EnumerateObject().Select(p => p.Name).ShouldBe(["items"]);
        var item = body.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["registrationId", "emailNormalized", "firstName", "lastName", "company", "contactId", "origin", "checkedInAt"],
            ignoreOrder: true);
        item.GetProperty("registrationId").GetGuid().ShouldBe(registration);
        item.GetProperty("emailNormalized").GetString().ShouldBe("maria@acme.com");
        item.GetProperty("firstName").GetString().ShouldBe("María");
        item.GetProperty("lastName").GetString().ShouldBe("García");
        item.GetProperty("company").GetString().ShouldBe("Viajes del Caribe SRL");
        item.GetProperty("contactId").GetGuid().ShouldBe(contact);
        item.GetProperty("checkedInAt").GetDateTimeOffset().ShouldBe(DoorOpens);
    }

    /// <summary>
    /// Un evento sin nada, o que este servicio no conoce, es una puerta vacía: <c>200</c>. No se le
    /// pregunta a events-service, que es quien está llamando (decisión 6 de PGTI-130).
    /// </summary>
    [Fact]
    public async Task List_Returns200WithNoItems_WhenTheEventHasNoRegistrations()
    {
        var response = await ListAsync(Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadJsonAsync(response)).GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task List_LeavesOutCheckInsOnAnotherEvent_WhenRead()
    {
        var eventId = Guid.NewGuid();
        var mine = await SeedRegistrationAsync(eventId, "mia@acme.com", checkedInAt: DoorOpens);
        await SeedRegistrationAsync(Guid.NewGuid(), "otra@acme.com", checkedInAt: DoorOpens);

        var item = (await ItemsAsync(eventId)).ShouldHaveSingleItem();

        item.GetProperty("registrationId").GetGuid().ShouldBe(mine);
    }

    [Fact]
    public async Task List_Returns200_WhenTheCallerHoldsOnlyCheckInServiceRead()
    {
        var response = await ListAsync(Guid.NewGuid(), roles: AuthorizationPolicies.CheckInServiceRead);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Ningún rol de persona abre esta lista, ni el de la puerta ni el del personal (§7).</summary>
    [Theory]
    [InlineData(AuthorizationPolicies.CheckInWrite)]
    [InlineData(AuthorizationPolicies.CheckInRead)]
    [InlineData(AuthorizationPolicies.PlatformStaff)]
    public async Task List_Returns403_WhenTheCallerHoldsOnlyAnotherRole(string role)
    {
        var response = await ListAsync(Guid.NewGuid(), roles: role);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static string Route(Guid eventId) => $"/v1/convocatorias/events/{eventId}/check-ins";

    private static string Origin(JsonElement item) => item.GetProperty("origin").GetString()!;

    private static string EmailNormalized(JsonElement item) => item.GetProperty("emailNormalized").GetString()!;

    private Task<HttpResponseMessage> ListAsync(Guid eventId, string? roles = null)
    {
        var client = _factory.CreateClient();
        if (roles is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client.GetAsync(Route(eventId));
    }

    private async Task<List<JsonElement>> ItemsAsync(Guid eventId)
    {
        var response = await ListAsync(eventId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray()];
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Una campaña del evento, lanzada o en borrador; solo la estampa de lanzamiento las separa.</summary>
    private async Task<Guid> SeedCampaignAsync(Guid eventId, bool launched)
    {
        var id = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();
        var state = launched ? CampaignState.Sent : CampaignState.Draft;
        Guid? launcher = launched ? actor : null;
        DateTimeOffset? launchedAt = launched ? DateTimeOffset.UtcNow.AddHours(-3) : null;
        await ExecuteAsync($"""
            INSERT INTO convocatorias.campaign
                (id, event_id, template_key, language, state, launched_by_id, launched_at,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES ({id}, {eventId}, 'registration_invite', 'es', {state}, {launcher}, {launchedAt},
                    now(), now(), {actor}, {actor})
            """);
        return id;
    }

    private async Task<Guid> SeedRecipientAsync(
        Guid campaignId,
        string email,
        string sendStatus = RecipientSendStatus.HandedOff,
        Guid? contactId = null)
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync($"""
            INSERT INTO convocatorias.campaign_recipient
                (id, campaign_id, contact_id, first_name, last_name, email, send_status, created_at, updated_at)
            VALUES ({id}, {campaignId}, {contactId ?? Guid.CreateVersion7()}, 'Ana', 'Gómez', {email}, {sendStatus},
                    now(), now())
            """);
        return id;
    }

    /// <summary>
    /// Una inscripción, con su check-in si se pide. Sin <paramref name="crmContactId"/> queda pendiente
    /// de sincronizar con el CRM, que es la pareja de columnas de la spec <c>06</c> §4.3.
    /// </summary>
    private async Task<Guid> SeedRegistrationAsync(
        Guid eventId,
        string email,
        DateTimeOffset? checkedInAt = null,
        Guid? campaignRecipientId = null,
        Guid? crmContactId = null)
    {
        var id = Guid.CreateVersion7();
        var unique = Guid.NewGuid().ToString("N");
        Guid? checkedInBy = checkedInAt is null ? null : Guid.CreateVersion7();
        DateTimeOffset? crmSyncFailedAt = crmContactId is null ? DateTimeOffset.UtcNow : null;
        await ExecuteAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, campaign_recipient_id, first_name, last_name, email, email_normalized, company,
                 reference, qr_code, contact_details_differ, crm_contact_id, crm_sync_failed_at,
                 checked_in_at, checked_in_by_id, created_at, updated_at)
            VALUES ({id}, {eventId}, {campaignRecipientId}, 'María', 'García', {email}, {email.Trim().ToLowerInvariant()},
                    'Viajes del Caribe SRL', {"MITUR-2026-" + unique}, {unique}, false, {crmContactId},
                    {crmSyncFailedAt}, {checkedInAt}, {checkedInBy}, now(), now())
            """);
        return id;
    }

    private async Task ExecuteAsync(FormattableString sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync(sql);
    }
}
