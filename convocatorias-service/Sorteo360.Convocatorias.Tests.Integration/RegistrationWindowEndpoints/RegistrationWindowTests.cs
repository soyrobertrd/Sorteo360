using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.RegistrationWindowEndpoints;

/// <summary>
/// La ventana de inscripción (spec <c>06</c> §5): una persona cierra la puerta, el evento sigue en
/// pie y en la cartelera, y nadie recibe nada.
/// <para>
/// <b>Cada test sirve su propio evento</b>: la tabla vive toda la clase y la fila de uno sería la
/// ventana cerrada del siguiente.
/// </para>
/// </summary>
public sealed class RegistrationWindowTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public RegistrationWindowTests(RegistrationPortalFactory factory) => _factory = factory;

    #region Cerrar y reabrir

    [Fact]
    public async Task CloseRegistrations_ReturnsTheWindowShutAndInsertsItsRow_WhenTheEventHasNoRow()
    {
        var eventId = ServeAPublicEvent();
        var closer = Guid.NewGuid();
        (await RowsAsync(eventId)).ShouldBe(0);

        var response = await CloseRegistrationsAsync(eventId, closer);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var window = await BodyAsync(response);
        window.GetProperty("eventId").GetGuid().ShouldBe(eventId);
        window.GetProperty("open").GetBoolean().ShouldBeFalse();
        window.GetProperty("closedById").GetGuid().ShouldBe(closer);
        window.GetProperty("closedAt").ValueKind.ShouldBe(JsonValueKind.String);
        (await RowsAsync(eventId)).ShouldBe(1);
    }

    /// <summary>
    /// Idempotente en efecto (§5.2): cerrar lo cerrado no cambia nada, ni la hora ni quién.
    /// </summary>
    [Fact]
    public async Task CloseRegistrations_ReturnsTheFirstStamps_WhenTheWindowIsAlreadyShut()
    {
        var eventId = ServeAPublicEvent();
        var first = await BodyAsync(await CloseRegistrationsAsync(eventId, Guid.NewGuid()));

        var second = await BodyAsync(await CloseRegistrationsAsync(eventId, Guid.NewGuid()));

        second.GetProperty("closedAt").GetDateTimeOffset()
            .ShouldBe(first.GetProperty("closedAt").GetDateTimeOffset());
        second.GetProperty("closedById").GetGuid()
            .ShouldBe(first.GetProperty("closedById").GetGuid());
    }

    [Fact]
    public async Task ReopenRegistrations_ReturnsTheWindowOpenWithItsRowKept_WhenTheWindowWasShut()
    {
        var eventId = ServeAPublicEvent();
        var reopener = Guid.NewGuid();
        await CloseRegistrationsAsync(eventId, Guid.NewGuid());
        var client = _factory.CreateClient();
        (await RegistrationTestClient.RegisterAsync(client, eventId, email: Unique()))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var response = await ReopenRegistrationsAsync(eventId, reopener);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var window = await BodyAsync(response);
        window.GetProperty("open").GetBoolean().ShouldBeTrue();
        window.GetProperty("closedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        window.GetProperty("reopenedById").GetGuid().ShouldBe(reopener);
        window.GetProperty("reopenedAt").ValueKind.ShouldBe(JsonValueKind.String);
        (await RowsAsync(eventId)).ShouldBe(1);

        (await RegistrationTestClient.RegisterAsync(client, eventId, email: Unique()))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>
    /// Reabrir lo que nunca se cerró no escribe fila: la ausencia ya significa abierta, y una fila
    /// por evento visitado es el primer paso hacia una copia local de <c>Event</c> (§5.1).
    /// </summary>
    [Fact]
    public async Task ReopenRegistrations_ReturnsTheWindowOpenWithoutARow_WhenItWasNeverShut()
    {
        var eventId = ServeAPublicEvent();

        var window = await BodyAsync(await ReopenRegistrationsAsync(eventId, Guid.NewGuid()));

        window.GetProperty("open").GetBoolean().ShouldBeTrue();
        window.GetProperty("reopenedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        (await RowsAsync(eventId)).ShouldBe(0);
    }

    /// <summary><c>campaigns:write</c> y no <c>campaigns:launch</c> (§5.2).</summary>
    [Theory]
    [InlineData("close-registrations")]
    [InlineData("reopen-registrations")]
    public async Task CloseAndReopenRegistrations_Returns403_WhenTheCallerLacksCampaignsWrite(string action)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Route(Guid.NewGuid(), action));
        request.Headers.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsLaunch);

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region La cartelera con la puerta cerrada

    [Fact]
    public async Task ListPublicEvents_ReturnsTheEventWithRegistrationOpenFalse_WhenTheWindowIsShut()
    {
        var shut = Guid.NewGuid();
        var open = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(shut), PublicEvent(open));
        await CloseRegistrationsAsync(shut, Guid.NewGuid());

        var listed = await BodyAsync(await _factory.CreateClient().GetAsync("/v1/public/events"));

        RegistrationOpenOf(listed, shut).ShouldBeFalse();
        RegistrationOpenOf(listed, open).ShouldBeTrue();
    }

    [Fact]
    public async Task GetPublicEvent_Returns200WithRegistrationOpenFalse_WhenTheWindowIsShut()
    {
        var eventId = ServeAPublicEvent();
        await CloseRegistrationsAsync(eventId, Guid.NewGuid());

        var response = await _factory.CreateClient().GetAsync($"/v1/public/events/{eventId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BodyAsync(response)).GetProperty("registrationOpen").GetBoolean().ShouldBeFalse();
    }

    #endregion

    #region Las dos escrituras del portal

    [Fact]
    public async Task Resolve_Returns409RegistrationsClosed_WhenTheWindowIsShut()
    {
        var eventId = ServeAPublicEvent();
        var invitation = await RegistrationTestClient.InviteAsync(_factory, eventId, email: Unique());
        await CloseRegistrationsAsync(eventId, Guid.NewGuid());

        var response = await RegistrationTestClient.ResolveAsync(_factory.CreateClient(), invitation.Token);

        await ShouldBeShutAsync(response);
    }

    /// <summary>
    /// Un token privado tampoco resuelve con su relleno (§5.4): el invitado aterriza en la página
    /// de la puerta cerrada y no en un formulario que no puede mandar.
    /// </summary>
    [Fact]
    public async Task Resolve_Returns409RegistrationsClosed_WhenAPrivateTokenMeetsAShutWindow()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId) with { Visibility = EventsStub.PrivateVisibility });
        var invitation = await RegistrationTestClient.InviteAsync(_factory, eventId, email: Unique());
        await CloseRegistrationsAsync(eventId, Guid.NewGuid());

        var response = await RegistrationTestClient.ResolveAsync(_factory.CreateClient(), invitation.Token);

        await ShouldBeShutAsync(response);
    }

    /// <summary>
    /// Un token privado ya gastado sigue saliendo por el <c>404</c> compartido aunque la puerta
    /// esté cerrada: un <c>409</c> ahí le confirmaría a quien lo trae que el token fue real.
    /// </summary>
    [Fact]
    public async Task Resolve_Returns404_WhenASpentPrivateTokenMeetsAShutWindow()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId) with { Visibility = EventsStub.PrivateVisibility });
        var invitation = await RegistrationTestClient.InviteAsync(_factory, eventId, email: Unique());
        var client = _factory.CreateClient();
        (await RegistrationTestClient.RegisterAsync(client, eventId, invitation.Token, email: invitation.Email))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        await CloseRegistrationsAsync(eventId, Guid.NewGuid());

        var response = await RegistrationTestClient.ResolveAsync(client, invitation.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await BodyAsync(response)).GetProperty("code").GetString()
            .ShouldBe(PublicRegistrationNotFoundError.ErrorCode);
    }

    [Fact]
    public async Task Register_Returns409RegistrationsClosedAndWritesNothing_WhenTheWindowIsShut()
    {
        var eventId = ServeAPublicEvent();
        await CloseRegistrationsAsync(eventId, Guid.NewGuid());

        var response = await RegistrationTestClient.RegisterAsync(_factory.CreateClient(), eventId, email: Unique());

        await ShouldBeShutAsync(response);
        (await RegistrationTestClient.CountAsync(_factory, eventId)).ShouldBe(0);
    }

    /// <summary>
    /// Quien ya estaba inscrito y reenvía su formulario —perdió el correo— recibe su inscripción
    /// de siempre: la puerta cerrada frena altas, no a quien ya entró (§4.1).
    /// </summary>
    [Fact]
    public async Task Register_Returns200WithTheExistingRegistration_WhenTheWindowIsShutAfterIt()
    {
        var eventId = ServeAPublicEvent();
        var client = _factory.CreateClient();
        var email = Unique();
        var first = await BodyAsync(await RegistrationTestClient.RegisterAsync(client, eventId, email: email));
        await CloseRegistrationsAsync(eventId, Guid.NewGuid());

        var response = await RegistrationTestClient.RegisterAsync(client, eventId, email: email);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BodyAsync(response)).GetProperty("reference").GetString()
            .ShouldBe(first.GetProperty("reference").GetString());
    }

    #endregion

    #region Lo que la puerta no toca

    [Fact]
    public async Task GetTicketAndQr_Returns200_WhenTheWindowIsShutAfterTheRegistration()
    {
        var eventId = ServeAPublicEvent();
        var client = _factory.CreateClient();
        var qrCode = (await BodyAsync(await RegistrationTestClient.RegisterAsync(client, eventId, email: Unique())))
            .GetProperty("qrCode").GetString()!;
        await CloseRegistrationsAsync(eventId, Guid.NewGuid());

        (await RegistrationTestClient.TicketAsync(client, qrCode)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RegistrationTestClient.QrAsync(client, qrCode)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unsubscribe_Returns200_WhenTheWindowIsShut()
    {
        var eventId = ServeAPublicEvent();
        await CloseRegistrationsAsync(eventId, Guid.NewGuid());
        _factory.Crm.ServeDefaults();
        var seeded = await UnsubscribeTestClient.InviteAsync(_factory, email: Unique());

        var response = await UnsubscribeTestClient.UnsubscribeAsync(_factory.CreateClient(), seeded.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Helpers

    private static string Route(Guid eventId, string action) =>
        $"/v1/convocatorias/events/{eventId}/{action}";

    private Task<HttpResponseMessage> CloseRegistrationsAsync(Guid eventId, Guid actor) =>
        PostAsync(Route(eventId, "close-registrations"), actor);

    private Task<HttpResponseMessage> ReopenRegistrationsAsync(Guid eventId, Guid actor) =>
        PostAsync(Route(eventId, "reopen-registrations"), actor);

    private Task<HttpResponseMessage> PostAsync(string route, Guid actor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Add(TestAuthHandler.SubHeader, actor.ToString());

        return _factory.CreateClient().SendAsync(request);
    }

    private Guid ServeAPublicEvent()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(PublicEvent(eventId));
        return eventId;
    }

    private static StubEvent PublicEvent(Guid id) => new(
        id, "Roadshow Madrid 2026", EventsStub.PublicVisibility, "es", StubEventStatus.InProgress);

    private static async Task ShouldBeShutAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BodyAsync(response)).GetProperty("code").GetString().ShouldBe(RegistrationsClosedError.ErrorCode);
    }

    private static bool RegistrationOpenOf(JsonElement list, Guid eventId) =>
        list.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == eventId)
            .GetProperty("registrationOpen")
            .GetBoolean();

    private async Task<int> RowsAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM convocatorias.registration_window
                WHERE event_id = {eventId}
                """)
            .SingleAsync();
    }

    private static string Unique() =>
        EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    #endregion
}
