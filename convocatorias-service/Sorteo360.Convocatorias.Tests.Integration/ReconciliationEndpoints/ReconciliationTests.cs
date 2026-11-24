using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;
using Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.ReconciliationEndpoints;

/// <summary>
/// La pantalla de reconciliación (spec <c>07</c> §6): cinco conteos y un listado paginado por cubo.
/// <para>
/// <b>Los conteos son de todo el servicio y la base vive toda la clase</b>, así que cada test
/// compara contra lo que había antes de actuar en vez de contra un número fijo, y busca su propia
/// fila en el cubo por id.
/// </para>
/// </summary>
public sealed class ReconciliationTests : IClassFixture<RegistrationPortalFactory>
{
    private const string Route = "/v1/convocatorias/reconciliation";

    private readonly RegistrationPortalFactory _factory;

    public ReconciliationTests(RegistrationPortalFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
    }

    [Fact]
    public async Task Summary_ReturnsOneMorePendingRegistrationSync_WhenCrmRefusedTheContactCreate()
    {
        var eventId = ServeOpenEvent();
        var before = await SummaryAsync();
        _factory.Crm.FailContactCreateAfter(0);
        var email = Unique();

        await RegisterAsync(eventId, email);

        var after = await SummaryAsync();
        after.GetProperty("pendingRegistrationSync").GetInt32()
            .ShouldBe(before.GetProperty("pendingRegistrationSync").GetInt32() + 1);
        var row = (await BucketRowsAsync("pending-registration-sync"))
            .Where(item => item.GetProperty("email").GetString() == email).ShouldHaveSingleItem();
        row.GetProperty("eventId").GetGuid().ShouldBe(eventId);
        row.GetProperty("crmContactId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>
    /// No hay nada que reintentar: el arreglo es una persona editando el CRM. Dar la fila por
    /// revisada la saca del cubo, y el conteo baja (spec <c>07</c> §6.1).
    /// </summary>
    [Fact]
    public async Task AcknowledgeDetails_Returns200AndClearsTheRowFromTheBucket_WhenTheDetailsDiffer()
    {
        var eventId = ServeOpenEvent();
        var registrationId = await RegisterAsync(eventId, CrmStub.ContactEmail, firstName: "J.", lastName: "Perez");
        var flagged = (await SummaryAsync()).GetProperty("detailsDiffer").GetInt32();
        (await BucketRowsAsync("details-differ")).ShouldContain(row => row.GetProperty("id").GetGuid() == registrationId);
        var reviewer = Guid.NewGuid();

        var response = await AcknowledgeAsync(registrationId, reviewer);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detailsReviewedById").GetGuid().ShouldBe(reviewer);
        body.GetProperty("detailsReviewedAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
        (await BucketRowsAsync("details-differ")).ShouldNotContain(row => row.GetProperty("id").GetGuid() == registrationId);
        (await SummaryAsync()).GetProperty("detailsDiffer").GetInt32().ShouldBe(flagged - 1);
    }

    /// <summary>Apretarlo dos veces no cambia quién la revisó primero.</summary>
    [Fact]
    public async Task AcknowledgeDetails_ReturnsTheFirstReviewer_WhenAcknowledgedTwice()
    {
        var registrationId = await RegisterAsync(ServeOpenEvent(), CrmStub.ContactEmail, firstName: "J.", lastName: "Perez");
        var first = Guid.NewGuid();
        (await AcknowledgeAsync(registrationId, first)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await AcknowledgeAsync(registrationId, Guid.NewGuid());

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detailsReviewedById").GetGuid().ShouldBe(first);
    }

    [Fact]
    public async Task AcknowledgeDetails_Returns409_WhenTheDetailsDoNotDiffer()
    {
        var registrationId = await RegisterAsync(ServeOpenEvent(), Unique());

        var response = await AcknowledgeAsync(registrationId, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .ShouldBe(RegistrationDetailsDoNotDifferError.ErrorCode);
    }

    [Fact]
    public async Task AcknowledgeDetails_Returns404_WhenTheRegistrationDoesNotExist()
    {
        (await AcknowledgeAsync(Guid.NewGuid(), Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// La baja que el CRM no llegó a recibir. Es de la persona y no de la campaña, así que el cubo
    /// es de todo el servicio (spec <c>07</c> §6.2).
    /// </summary>
    [Fact]
    public async Task PendingUnsubscribes_ReturnsTheRecipientWithItsCampaign_WhenTheUnsubscribeNeverReachedCrm()
    {
        var before = (await SummaryAsync()).GetProperty("pendingUnsubscribes").GetInt32();
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory, email: Unique());
        await ExecuteAsync($"""
            UPDATE convocatorias.campaign_recipient SET unsubscribed_at = now() WHERE id = {seeded.RecipientId}
            """);

        var row = (await BucketRowsAsync("pending-unsubscribes"))
            .Where(item => item.GetProperty("recipientId").GetGuid() == seeded.RecipientId).ShouldHaveSingleItem();

        row.GetProperty("contactId").GetGuid().ShouldBe(seeded.ContactId);
        row.GetProperty("email").GetString().ShouldBe(seeded.Email);
        row.GetProperty("eventId").GetGuid().ShouldNotBe(Guid.Empty);
        (await SummaryAsync()).GetProperty("pendingUnsubscribes").GetInt32().ShouldBe(before + 1);
    }

    /// <summary>
    /// "Seleccioné 240 y veo 227, ¿quién falta?" (spec <c>07</c> §6.4): la copia del nombre, el
    /// código del motivo y cuándo.
    /// </summary>
    [Fact]
    public async Task Exclusions_ReturnsTheNameSnapshotReasonAndExcludedAt_WhenSelectionExcludedAContact()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);
        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await client.GetAsync($"/v1/convocatorias/campaigns/{campaign.Id}/exclusions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().ShouldBe(1);
        var row = body.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        row.GetProperty("contactId").GetGuid().ShouldBe(CrmStub.SuppressedContactId);
        row.GetProperty("firstName").GetString().ShouldBe("Luis");
        row.GetProperty("lastName").GetString().ShouldBe("Gómez");
        row.GetProperty("reason").GetString().ShouldBe(ExclusionReason.OptedOut);
        row.GetProperty("excludedAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Exclusions_Returns404_WhenTheCampaignDoesNotExist()
    {
        var response = await _factory.CreateClient().GetAsync($"/v1/convocatorias/campaigns/{Guid.NewGuid()}/exclusions");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/pending-registration-sync")]
    [InlineData("/pending-unsubscribes")]
    [InlineData("/details-differ")]
    [InlineData("/stranded-campaigns")]
    [InlineData("/uncancelled-notices")]
    public async Task Reconciliation_Returns403_WhenTheCallerLacksConvocatoriasRead(string bucket)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsWrite);

        (await client.GetAsync($"{Route}{bucket}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Exclusions_Returns403_WhenTheCallerLacksConvocatoriasRead()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsWrite);

        (await client.GetAsync($"/v1/convocatorias/campaigns/{Guid.NewGuid()}/exclusions"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AcknowledgeDetails_Returns403_WhenTheCallerLacksCampaignsWrite()
    {
        var registrationId = await RegisterAsync(ServeOpenEvent(), CrmStub.ContactEmail, firstName: "J.", lastName: "Perez");

        var response = await AcknowledgeAsync(registrationId, Guid.NewGuid(), roles: AuthorizationPolicies.ConvocatoriasRead);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await BucketRowsAsync("details-differ")).ShouldContain(row => row.GetProperty("id").GetGuid() == registrationId);
    }

    /// <summary>
    /// El segundo llamado del diálogo de cancelar no llegó (spec <c>07</c> §6.5). El evento aparece
    /// con sus inscritos sin avisar, y el conteo vuelve a cero cuando el aviso corre.
    /// </summary>
    [Fact]
    public async Task UncancelledNotices_ReturnsTheEventUntilTheCancellationNoticeRuns_WhenTheEventWasCancelled()
    {
        var eventId = ServeOpenEvent();
        await RegisterAsync(eventId, Unique());
        await RegisterAsync(eventId, Unique());
        _factory.Events.ServeEvents(new StubEvent(
            eventId, EventsStub.EventName, EventsStub.PublicVisibility, EventsStub.EventLanguage,
            StubEventStatus.Cancelled));

        (await SummaryAsync()).GetProperty("uncancelledNotices").GetInt32().ShouldBe(1);
        var row = (await BucketRowsAsync("uncancelled-notices")).ShouldHaveSingleItem();
        row.GetProperty("eventId").GetGuid().ShouldBe(eventId);
        row.GetProperty("unnotified").GetInt32().ShouldBe(2);
        row.GetProperty("cancelledAt").GetDateTimeOffset().ShouldBe(EventsStub.UpdatedAt);

        var notice = await _factory.CreateClient()
            .PostAsync($"/v1/convocatorias/events/{eventId}/cancellation-notice", content: null);
        notice.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await SummaryAsync()).GetProperty("uncancelledNotices").GetInt32().ShouldBe(0);
        (await BucketRowsAsync("uncancelled-notices")).ShouldBeEmpty();
    }

    #region Ayudas

    private Guid ServeOpenEvent()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(new StubEvent(
            eventId, EventsStub.EventName, EventsStub.PublicVisibility, EventsStub.EventLanguage,
            StubEventStatus.InProgress));
        return eventId;
    }

    private async Task<Guid> RegisterAsync(Guid eventId, string email, string firstName = "Luis", string lastName = "Peña")
    {
        var response = await RegistrationTestClient.RegisterAsync(
            _factory.CreateClient(), eventId, email: email, firstName: firstName, lastName: lastName);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await ScalarAsync<Guid>($"""
            SELECT id AS "Value" FROM convocatorias.registration
            WHERE event_id = {eventId} AND email_normalized = {email}
            """);
    }

    private Task<HttpResponseMessage> AcknowledgeAsync(Guid registrationId, Guid reviewer, string? roles = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/v1/convocatorias/registrations/{registrationId}/acknowledge-details");
        request.Headers.Add(TestAuthHandler.SubHeader, reviewer.ToString());

        if (roles is not null)
            request.Headers.Add(TestAuthHandler.RolesHeader, roles);

        return _factory.CreateClient().SendAsync(request);
    }

    private async Task<JsonElement> SummaryAsync()
    {
        var response = await _factory.CreateClient().GetAsync(Route);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Todas las filas de un cubo, en la página más grande que el servicio admite.</summary>
    private async Task<IReadOnlyList<JsonElement>> BucketRowsAsync(string bucket)
    {
        var response = await _factory.CreateClient().GetAsync($"{Route}/{bucket}?pageSize=100");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return [.. body.GetProperty("items").EnumerateArray()];
    }

    private async Task<T> ScalarAsync<T>(FormattableString sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database.SqlQuery<T>(sql).SingleAsync();
    }

    private async Task ExecuteAsync(FormattableString sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        await context.Database.ExecuteSqlAsync(sql);
    }

    private static string Unique() => EmailAddressShape.Normalize($"r{Guid.NewGuid():N}@acme.com");

    #endregion
}
