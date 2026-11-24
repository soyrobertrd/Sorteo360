using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CancellationNoticeEndpoints;

/// <summary>
/// El aviso de cancelación (spec <c>06</c> §4.7): cancelar un evento le escribe a cada inscrito
/// una vez, y nada más. El transporte de los tests es el stub, así que "le escribe" se comprueba
/// leyendo <c>stub_notification</c>.
/// <para>
/// <b>Cada test sirve su propio evento</b>: se inscribe gente mientras está vivo y después se
/// sirve el mismo id cancelado, que es el orden en que ocurre de verdad.
/// </para>
/// </summary>
public sealed class CancellationNoticeTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public CancellationNoticeTests(RegistrationPortalFactory factory) => _factory = factory;

    [Fact]
    public async Task NotifyCancellation_Returns202AndMailsEveryRegistrationOnce_WhenTheEventIsCancelled()
    {
        var eventId = ServeEvent(StubEventStatus.InProgress);
        var invitation = await RegistrationTestClient.InviteAsync(_factory, eventId, email: Unique());
        var client = _factory.CreateClient();
        var walkIn = await RegisterAsync(client, eventId, Unique());
        var referral = await RegisterAsync(client, eventId, invitation.Email, invitation.Token);
        ServeEvent(StubEventStatus.Cancelled, eventId);

        var response = await NotifyAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await BodyAsync(response);
        body.GetProperty("eventId").GetGuid().ShouldBe(eventId);
        body.GetProperty("notified").GetInt32().ShouldBe(2);
        body.GetProperty("failed").GetInt32().ShouldBe(0);

        (await NoticesForAsync(walkIn.Email)).ShouldHaveSingleItem().BodyHtml.ShouldContain(walkIn.Reference);
        (await NoticesForAsync(referral.Email)).ShouldHaveSingleItem().BodyHtml.ShouldContain(referral.Reference);
        (await UnnotifiedAsync(eventId)).ShouldBe(0);
    }

    /// <summary>
    /// La audiencia es quien se inscribió, no quien recibió la invitación (spec <c>06</c> §4.7).
    /// </summary>
    [Fact]
    public async Task NotifyCancellation_Returns202WithoutMailingTheInvitee_WhenTheInviteeNeverRegistered()
    {
        var eventId = ServeEvent(StubEventStatus.InProgress);
        var invitation = await RegistrationTestClient.InviteAsync(_factory, eventId, email: Unique());
        var person = await RegisterAsync(_factory.CreateClient(), eventId, Unique());
        ServeEvent(StubEventStatus.Cancelled, eventId);

        var response = await NotifyAsync(eventId);

        (await BodyAsync(response)).GetProperty("notified").GetInt32().ShouldBe(1);
        (await NoticesForAsync(person.Email)).ShouldHaveSingleItem();
        (await NoticesForAsync(invitation.Email)).ShouldBeEmpty();
    }

    [Fact]
    public async Task NotifyCancellation_ReturnsNoneNotifiedAndKeepsTheStamps_WhenCalledASecondTime()
    {
        var eventId = ServeEvent(StubEventStatus.InProgress);
        var client = _factory.CreateClient();
        var person = await RegisterAsync(client, eventId, Unique());
        ServeEvent(StubEventStatus.Cancelled, eventId);
        (await NotifyAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var firstStamp = await StampOfAsync(eventId, person.Email);

        var second = await NotifyAsync(eventId);

        second.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await BodyAsync(second)).GetProperty("notified").GetInt32().ShouldBe(0);
        (await NoticesForAsync(person.Email)).Count.ShouldBe(1);
        (await StampOfAsync(eventId, person.Email)).ShouldBe(firstStamp);
    }

    /// <summary>
    /// Un evento vivo, y uno cerrado: cerrar significa que ocurrió, y avisar ahí le diría a quien
    /// asistió que se canceló (spec <c>06</c> §4.7).
    /// </summary>
    [Theory]
    [InlineData(StubEventStatus.InProgress)]
    [InlineData(StubEventStatus.Closed)]
    public async Task NotifyCancellation_Returns409AndMailsNobody_WhenTheEventIsNotCancelled(string status)
    {
        var eventId = ServeEvent(StubEventStatus.InProgress);
        var client = _factory.CreateClient();
        var person = await RegisterAsync(client, eventId, Unique());
        ServeEvent(status, eventId);

        var response = await NotifyAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BodyAsync(response)).GetProperty("code").GetString().ShouldBe(EventNotCancelledError.ErrorCode);
        (await NoticesForAsync(person.Email)).ShouldBeEmpty();
        (await StampOfAsync(eventId, person.Email)).ShouldBeNull();
    }

    /// <summary>
    /// La supresión gobierna lo que mandamos sin que nos lo pidan. El aviso es un deber: quien se
    /// dio de baja del marketing igual tiene que saber que su evento no va (spec <c>06</c> §4.7).
    /// </summary>
    [Fact]
    public async Task NotifyCancellation_Returns202WithTheSuppressedRegistrantNotified_WhenCrmSaysTheyCannotBeEmailed()
    {
        var eventId = ServeEvent(StubEventStatus.InProgress);
        _factory.Crm.ServeContacts(new StubContact(
            CrmStub.SuppressedContactId, "Luis", "Gómez", CrmStub.SuppressedContactEmail,
            CanBeEmailed: false, SuppressionReason: CrmStub.OptOutSuppressionReason));
        var client = _factory.CreateClient();
        await RegisterAsync(client, eventId, CrmStub.SuppressedContactEmail);
        ServeEvent(StubEventStatus.Cancelled, eventId);

        var response = await NotifyAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await BodyAsync(response)).GetProperty("notified").GetInt32().ShouldBe(1);
        (await NoticesForAsync(CrmStub.SuppressedContactEmail)).ShouldHaveSingleItem();
    }

    /// <summary>
    /// Sobre la fila sembrada y no sobre una constante del código: es lo que de verdad sale.
    /// </summary>
    [Fact]
    public async Task AddCancellationNotice_ReturnsABodyWithNoUnsubscribeLinkAndNoReason_WhenReadFromTheSeededRow()
    {
        var body = await SeededEventCancelledBodyAsync();

        var placeholders = PlaceholderScanner.Scan(body);
        placeholders.ShouldNotContain(SupportedPlaceholder.UnsubscribeLink);
        placeholders.ShouldBeSubsetOf(
        [
            SupportedPlaceholder.RecipientName,
            SupportedPlaceholder.EventName,
            SupportedPlaceholder.EventDates,
            SupportedPlaceholder.Reference,
        ]);
        placeholders.ShouldContain(SupportedPlaceholder.Reference);
        body.ShouldNotContain("motivo", Case.Insensitive);
        body.ShouldNotContain("razón", Case.Insensitive);
    }

    /// <summary>
    /// El QR no se toca: ni se revoca ni se sella nada, y la entrada y su imagen siguen sirviendo
    /// después del aviso. El escáner de la puerta todavía no existe (spec <c>06</c> §4.6).
    /// </summary>
    [Fact]
    public async Task Ticket_Returns200WithItsQr_WhenTheEventWasCancelledAndNotified()
    {
        var eventId = ServeEvent(StubEventStatus.InProgress);
        var client = _factory.CreateClient();
        var person = await RegisterAsync(client, eventId, Unique());
        ServeEvent(StubEventStatus.Cancelled, eventId);
        (await NotifyAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await RegistrationTestClient.TicketAsync(client, person.QrCode)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var qr = await RegistrationTestClient.QrAsync(client, person.QrCode);
        qr.StatusCode.ShouldBe(HttpStatusCode.OK);
        qr.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
    }

    [Fact]
    public async Task NotifyCancellation_Returns403AndMailsNobody_WhenTheCallerLacksCampaignsWrite()
    {
        var eventId = ServeEvent(StubEventStatus.InProgress);
        var client = _factory.CreateClient();
        var person = await RegisterAsync(client, eventId, Unique());
        ServeEvent(StubEventStatus.Cancelled, eventId);

        var response = await NotifyAsync(eventId, roles: AuthorizationPolicies.ConvocatoriasRead);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await NoticesForAsync(person.Email)).ShouldBeEmpty();
    }

    #region Ayudas

    private Guid ServeEvent(string status, Guid? id = null)
    {
        var eventId = id ?? Guid.NewGuid();
        _factory.Events.ServeEvents(new StubEvent(
            eventId, "Roadshow Madrid 2026", EventsStub.PublicVisibility, "es", status));
        return eventId;
    }

    private Task<HttpResponseMessage> NotifyAsync(Guid eventId, string? roles = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/v1/convocatorias/events/{eventId}/cancellation-notice");

        if (roles is not null)
            request.Headers.Add(TestAuthHandler.RolesHeader, roles);

        return _factory.CreateClient().SendAsync(request);
    }

    private static async Task<Registered> RegisterAsync(
        HttpClient client, Guid eventId, string email, string? token = null)
    {
        var response = await RegistrationTestClient.RegisterAsync(client, eventId, token: token, email: email);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await BodyAsync(response);
        return new Registered(
            email,
            body.GetProperty("reference").GetString()!,
            body.GetProperty("qrCode").GetString()!);
    }

    private static string Unique() => EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Los avisos de cancelación que el stub dejó para una dirección, en orden.</summary>
    private async Task<IReadOnlyList<NoticeRow>> NoticesForAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<NoticeRow>($"""
                SELECT body_html AS "BodyHtml"
                FROM convocatorias.stub_notification
                WHERE recipient_email = {email}
                  AND type = {CancellationNoticeService.NotificationType}
                ORDER BY created_at
                """)
            .ToListAsync();
    }

    private Task<DateTimeOffset?> StampOfAsync(Guid eventId, string email) =>
        ScalarAsync<DateTimeOffset?>($"""
            SELECT cancellation_notified_at AS "Value"
            FROM convocatorias.registration
            WHERE event_id = {eventId} AND email_normalized = {email}
            """);

    private Task<int> UnnotifiedAsync(Guid eventId) =>
        ScalarAsync<int>($"""
            SELECT COUNT(*)::int AS "Value"
            FROM convocatorias.registration
            WHERE event_id = {eventId} AND cancellation_notified_at IS NULL
            """);

    private Task<string> SeededEventCancelledBodyAsync() =>
        ScalarAsync<string>($"""
            SELECT body_html AS "Value"
            FROM convocatorias.template
            WHERE template_key = {TemplateKey.EventCancelled} AND language = {AllowedLanguage.Espanol}
            """);

    private async Task<T> ScalarAsync<T>(FormattableString sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database.SqlQuery<T>(sql).SingleAsync();
    }

    private sealed record Registered(string Email, string Reference, string QrCode);

    private sealed record NoticeRow(string BodyHtml);

    #endregion
}
