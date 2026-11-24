using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.TrackingEndpoints;

/// <summary>
/// El resumen de un evento (spec <c>07</c> §2): la pantalla que todos abren primero.
/// <para>
/// Cada test siembra su propio evento por SQL, como la lista de participantes: llegar ahí por la API
/// pediría lanzar y esperar al worker.
/// </para>
/// </summary>
public sealed class TrackingEndpointsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public TrackingEndpointsTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task GetTracking_Returns403_WhenTheCallerLacksConvocatoriasRead()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsWrite);

        var response = await client.GetAsync(Route(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Convocatorias no le pregunta a nadie si el evento existe (§5, §7): un evento creado esta mañana
    /// no es un evento que falta, así que un id desconocido es un resumen en cero con la puerta abierta.
    /// </summary>
    [Fact]
    public async Task GetTracking_Returns200WithZerosAndAnOpenWindow_WhenTheEventIsUnknown()
    {
        _factory.Crm.ResetRequestLog();
        _factory.Events.ResetRequestLog();

        var tracking = await GetAsync(Guid.NewGuid());

        tracking.GetProperty("campaigns").GetArrayLength().ShouldBe(0);
        Count(tracking, "people", "invited").ShouldBe(0);
        Count(tracking, "registrations", "total").ShouldBe(0);
        tracking.GetProperty("notRegistered").GetInt32().ShouldBe(0);
        tracking.GetProperty("needsAttention").GetInt32().ShouldBe(0);
        var window = tracking.GetProperty("registrationWindow");
        window.GetProperty("open").GetBoolean().ShouldBeTrue();
        window.GetProperty("closedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        window.GetProperty("closedById").ValueKind.ShouldBe(JsonValueKind.Null);
        _factory.Crm.RequestCount.ShouldBe(0);
        _factory.Events.RequestCount.ShouldBe(0);
    }

    /// <summary>
    /// La persona a la que dos campañas le escribieron es una persona (§2), y la que falló en una y
    /// salió en la otra está entre los invitados, no entre los fallidos.
    /// </summary>
    [Fact]
    public async Task GetTracking_ReturnsOneInvited_WhenTwoCampaignsInvitedTheSamePerson()
    {
        var eventId = Guid.NewGuid();
        var first = await SeedCampaignAsync(eventId);
        var second = await SeedCampaignAsync(eventId);
        await SeedRecipientAsync(first, "juan@empresa.com", RecipientSendStatus.Failed);
        await SeedRecipientAsync(second, "Juan@Empresa.com");

        var tracking = await GetAsync(eventId);

        Count(tracking, "people", "invited").ShouldBe(1);
        Count(tracking, "people", "failed").ShouldBe(0);
        tracking.GetProperty("notRegistered").GetInt32().ShouldBe(1);
        tracking.GetProperty("campaigns").EnumerateArray().Select(c => c.GetProperty("recipientCount").GetInt32())
            .ShouldBe([1, 1]);
    }

    /// <summary>
    /// Un borrador ya tiene filas de gente a la que nadie decidió escribirle (§2): no aparece en la
    /// lista de campañas ni en ningún conteo, ni siquiera en <c>pendingSend</c>.
    /// </summary>
    [Fact]
    public async Task GetTracking_ReturnsNoTraceOfTheDraft_WhenADraftHoldsRecipients()
    {
        var eventId = Guid.NewGuid();
        var draft = await SeedUnlaunchedCampaignAsync(eventId, CampaignState.Draft);
        await SeedRecipientAsync(draft, "borrador@acme.com", RecipientSendStatus.Pending);
        await SeedRecipientAsync(draft, "otro@acme.com", RecipientSendStatus.Pending);
        var sent = await SeedCampaignAsync(eventId);
        await SeedRecipientAsync(sent, "enviada@acme.com");
        await SeedRegistrationAsync(eventId, "libre@acme.com");

        var tracking = await GetAsync(eventId);

        var campaign = tracking.GetProperty("campaigns").EnumerateArray().ShouldHaveSingleItem();
        campaign.GetProperty("id").GetGuid().ShouldBe(sent);
        campaign.GetProperty("state").GetString().ShouldBe(CampaignState.Sent);
        tracking.GetProperty("people").EnumerateObject().Select(p => (p.Name, p.Value.GetInt32()))
            .ShouldBe([("invited", 1), ("pendingSend", 0), ("failed", 0), ("suppressed", 0)]);
        Count(tracking, "registrations", "total").ShouldBe(1);
        Count(tracking, "registrations", "walkIn").ShouldBe(1);
        tracking.GetProperty("notRegistered").GetInt32().ShouldBe(1);
    }

    /// <summary>
    /// Una campaña cancelada desde borrador nunca salió, así que cuenta como el borrador que era
    /// (§2): sus filas <c>pending</c> no son gente varada de un envío cortado, son gente a la que
    /// nadie decidió escribirle.
    /// </summary>
    [Fact]
    public async Task GetTracking_ReturnsNoTraceOfTheCampaign_WhenItWasCancelledBeforeLaunch()
    {
        var eventId = Guid.NewGuid();
        var abandoned = await SeedUnlaunchedCampaignAsync(eventId, CampaignState.Cancelled);
        await SeedRecipientAsync(abandoned, "abandonada@acme.com", RecipientSendStatus.Pending);
        var sent = await SeedCampaignAsync(eventId);
        await SeedRecipientAsync(sent, "enviada@acme.com");

        var tracking = await GetAsync(eventId);

        tracking.GetProperty("campaigns").EnumerateArray().ShouldHaveSingleItem()
            .GetProperty("id").GetGuid().ShouldBe(sent);
        tracking.GetProperty("people").EnumerateObject().Select(p => (p.Name, p.Value.GetInt32()))
            .ShouldBe([("invited", 1), ("pendingSend", 0), ("failed", 0), ("suppressed", 0)]);
        tracking.GetProperty("notRegistered").GetInt32().ShouldBe(1);
    }

    /// <summary>
    /// Cancelada en pleno envío sí salió: las filas que quedaron en <c>pending</c> son exactamente
    /// las que el corte dejó sin mandar, y cuentan como hoy (§2).
    /// </summary>
    [Fact]
    public async Task GetTracking_ReturnsTheCampaignAndItsUnsentRowsAsPendingSend_WhenItWasCancelledAfterLaunch()
    {
        var eventId = Guid.NewGuid();
        var cut = await SeedCampaignAsync(eventId, CampaignState.Cancelled);
        await SeedRecipientAsync(cut, "mandada@acme.com");
        await SeedRecipientAsync(cut, "sin-mandar@acme.com", RecipientSendStatus.Pending);

        var tracking = await GetAsync(eventId);

        var campaign = tracking.GetProperty("campaigns").EnumerateArray().ShouldHaveSingleItem();
        campaign.GetProperty("id").GetGuid().ShouldBe(cut);
        campaign.GetProperty("state").GetString().ShouldBe(CampaignState.Cancelled);
        tracking.GetProperty("people").EnumerateObject().Select(p => (p.Name, p.Value.GetInt32()))
            .ShouldBe([("invited", 1), ("pendingSend", 1), ("failed", 0), ("suppressed", 0)]);
    }

    /// <summary>
    /// Lo que una persona tiene que ir a arreglar en este evento, con las definiciones de los cubos
    /// (§2, §6): una sincronización pendiente, una ficha sin revisar y una campaña varada. La ficha ya
    /// revisada, la baja pendiente, que es de todo el servicio, y lo de otro evento no suman.
    /// </summary>
    [Fact]
    public async Task GetTracking_ReturnsThisEventsSyncsUnreviewedDetailsAndStrandedCampaigns_WhenEachBucketHasARow()
    {
        var eventId = Guid.NewGuid();
        await SeedRegistrationAsync(eventId, "pendiente@acme.com", crmSyncPending: true);
        await SeedRegistrationAsync(eventId, "difiere@acme.com", detailsDiffer: true);
        await SeedRegistrationAsync(eventId, "revisada@acme.com", detailsDiffer: true, detailsReviewed: true);
        var stranded = await SeedCampaignAsync(eventId, CampaignState.Sending, DateTimeOffset.UtcNow.AddHours(-1));
        await SeedRecipientAsync(stranded, "varada@acme.com", RecipientSendStatus.Pending);
        var recent = await SeedCampaignAsync(eventId, CampaignState.Sending, DateTimeOffset.UtcNow);
        await SeedRecipientAsync(recent, "en-curso@acme.com", RecipientSendStatus.Pending);
        var unsubscribed = await SeedRecipientAsync(await SeedCampaignAsync(eventId), "baja@acme.com");
        await ExecuteAsync($"""
            UPDATE convocatorias.campaign_recipient SET unsubscribed_at = now() WHERE id = {unsubscribed}
            """);
        var otherEvent = Guid.NewGuid();
        await SeedRegistrationAsync(otherEvent, "otro-evento@acme.com", crmSyncPending: true);
        var otherStranded = await SeedCampaignAsync(otherEvent, CampaignState.Sending, DateTimeOffset.UtcNow.AddHours(-1));
        await SeedRecipientAsync(otherStranded, "otra-varada@acme.com", RecipientSendStatus.Pending);

        var tracking = await GetAsync(eventId);

        tracking.GetProperty("needsAttention").GetInt32().ShouldBe(3);
        Count(tracking, "people", "pendingSend").ShouldBe(2);
    }

    /// <summary>
    /// La ventana dice quién cerró la puerta y cuándo, y vuelve a abierta al reabrir (§2, <c>06</c>
    /// §5). Es una decisión que alguien ya tomó, así que no mueve <c>needsAttention</c>.
    /// </summary>
    [Fact]
    public async Task GetTracking_ReturnsTheCloserThenAnOpenWindowWithTheSameNeedsAttention_WhenRegistrationsCloseAndReopen()
    {
        var eventId = Guid.NewGuid();
        await SeedRegistrationAsync(eventId, "pendiente@acme.com", crmSyncPending: true);
        var closer = Guid.NewGuid();

        await PostWindowAsync(eventId, "close-registrations", closer);
        var shut = await GetAsync(eventId);
        await PostWindowAsync(eventId, "reopen-registrations", Guid.NewGuid());
        var reopened = await GetAsync(eventId);

        var window = shut.GetProperty("registrationWindow");
        window.GetProperty("open").GetBoolean().ShouldBeFalse();
        window.GetProperty("closedById").GetGuid().ShouldBe(closer);
        window.GetProperty("closedAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
        reopened.GetProperty("registrationWindow").GetProperty("open").GetBoolean().ShouldBeTrue();
        shut.GetProperty("needsAttention").GetInt32().ShouldBe(1);
        reopened.GetProperty("needsAttention").GetInt32().ShouldBe(1);
    }

    /// <summary>
    /// Lo que todavía no se sabe no viaja, ni como campo nulo (§5): un campo siempre nulo invita a
    /// pintar un cero. Se afirma una vez sobre las formas serializadas del resumen y de la
    /// reconciliación, con al menos una fila en cada cubo para que la afirmación mire algo.
    /// </summary>
    [Fact]
    public async Task GetTracking_ReturnsNoDeliveryFeedbackFieldHereOrInReconciliation_WhenEveryShapeHasARow()
    {
        var eventId = Guid.NewGuid();
        var sent = await SeedCampaignAsync(eventId, CampaignState.Sent, DateTimeOffset.UtcNow.AddHours(-2));
        var unsubscribed = await SeedRecipientAsync(sent, "baja@acme.com");
        await ExecuteAsync($"""
            UPDATE convocatorias.campaign_recipient SET unsubscribed_at = now() WHERE id = {unsubscribed};
            INSERT INTO convocatorias.campaign_exclusion
                (id, campaign_id, contact_id, first_name, last_name, email, reason, excluded_at)
            VALUES ({Guid.CreateVersion7()}, {sent}, {Guid.NewGuid()}, 'Ana', 'Gómez', NULL, 'no_email', now())
            """);
        var stranded = await SeedCampaignAsync(eventId, CampaignState.Sending, DateTimeOffset.UtcNow.AddHours(-1));
        await SeedRecipientAsync(stranded, "varada@acme.com", RecipientSendStatus.Pending);
        await SeedRegistrationAsync(eventId, "pendiente@acme.com", crmSyncPending: true);
        await SeedRegistrationAsync(eventId, "difiere@acme.com", detailsDiffer: true);
        _factory.Events.ServeEvents(new StubEvent(
            eventId, EventsStub.EventName, EventsStub.PublicVisibility, EventsStub.EventLanguage,
            StubEventStatus.Cancelled));

        var bodies = new List<string> { await ReadAsync(Route(eventId)), await ReadAsync("/v1/convocatorias/reconciliation") };
        foreach (var path in new[]
        {
            "reconciliation/pending-registration-sync", "reconciliation/pending-unsubscribes",
            "reconciliation/details-differ", "reconciliation/stranded-campaigns",
            "reconciliation/uncancelled-notices", $"campaigns/{sent}/exclusions",
        })
        {
            var page = await ReadAsync($"/v1/convocatorias/{path}");
            JsonDocument.Parse(page).RootElement.GetProperty("items").GetArrayLength().ShouldBeGreaterThan(0, path);
            bodies.Add(page);
        }

        foreach (var unknowable in new[] { "delivered", "opened", "clicked", "bounced" })
            bodies.ShouldAllBe(body => !body.Contains(unknowable, StringComparison.OrdinalIgnoreCase));
    }

    #region Ayudas

    private async Task<string> ReadAsync(string route)
    {
        var response = await _factory.CreateClient().GetAsync(route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, route);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task PostWindowAsync(Guid eventId, string action, Guid user)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/convocatorias/events/{eventId}/{action}");
        request.Headers.Add(TestAuthHandler.SubHeader, user.ToString());

        (await _factory.CreateClient().SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string Route(Guid eventId) => $"/v1/convocatorias/events/{eventId}/tracking";

    private async Task<JsonElement> GetAsync(Guid eventId)
    {
        var response = await _factory.CreateClient().GetAsync(Route(eventId));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static int Count(JsonElement tracking, string group, string name) =>
        tracking.GetProperty(group).GetProperty(name).GetInt32();

    /// <summary>Una campaña lanzada, como toda la que salió de borrador por la API.</summary>
    private Task<Guid> SeedCampaignAsync(
        Guid eventId,
        string state = CampaignState.Sent,
        DateTimeOffset? launchedAt = null) =>
        InsertCampaignAsync(eventId, state, launchedAt ?? DateTimeOffset.UtcNow.AddHours(-3));

    /// <summary>Un borrador, o una campaña cancelada sin haber salido nunca de borrador.</summary>
    private Task<Guid> SeedUnlaunchedCampaignAsync(Guid eventId, string state) =>
        InsertCampaignAsync(eventId, state, launchedAt: null);

    /// <summary>La fila tal cual: sin <paramref name="launchedAt"/> no lleva estampa de lanzamiento.</summary>
    private async Task<Guid> InsertCampaignAsync(Guid eventId, string state, DateTimeOffset? launchedAt)
    {
        var id = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();
        Guid? launcher = launchedAt is null ? null : actor;
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
        string sendStatus = RecipientSendStatus.HandedOff)
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync($"""
            INSERT INTO convocatorias.campaign_recipient
                (id, campaign_id, contact_id, first_name, last_name, email, send_status, created_at, updated_at)
            VALUES ({id}, {campaignId}, {Guid.CreateVersion7()}, 'Juan', 'Pérez', {email}, {sendStatus}, now(), now())
            """);
        return id;
    }

    /// <summary>
    /// Una inscripción ya sincronizada con el CRM salvo que se pida otra cosa: pendiente de
    /// sincronizar, o con datos que no coinciden y quizá ya revisados.
    /// </summary>
    private async Task SeedRegistrationAsync(
        Guid eventId,
        string email,
        bool crmSyncPending = false,
        bool detailsDiffer = false,
        bool detailsReviewed = false)
    {
        var unique = Guid.NewGuid().ToString("N");
        Guid? contactId = crmSyncPending ? null : Guid.NewGuid();
        DateTimeOffset? failedAt = crmSyncPending ? DateTimeOffset.UtcNow : null;
        DateTimeOffset? reviewedAt = detailsReviewed ? DateTimeOffset.UtcNow : null;
        await ExecuteAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, first_name, last_name, email, email_normalized, reference, qr_code,
                 crm_contact_id, crm_sync_failed_at, contact_details_differ, details_reviewed_at, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, {eventId}, 'Luis', 'Peña', {email}, {email.Trim().ToLowerInvariant()},
                    {"MITUR-2026-" + unique}, {unique}, {contactId}, {failedAt}, {detailsDiffer}, {reviewedAt}, now(), now())
            """);
    }

    private async Task ExecuteAsync(FormattableString sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync(sql);
    }

    #endregion
}
