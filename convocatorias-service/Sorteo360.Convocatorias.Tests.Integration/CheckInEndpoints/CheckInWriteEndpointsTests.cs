using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CheckInEndpoints;

/// <summary>
/// El escaneo de la puerta y su deshecha (spec <c>09</c> §3 y §4, PGTI-127). Cada test siembra su
/// inscripción por SQL en un evento recién inventado, así que ninguno ve las filas de otro.
/// </summary>
public sealed class CheckInWriteEndpointsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public CheckInWriteEndpointsTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task CheckIn_Returns201Then200WithTheFirstTime_WhenTheSameCodeIsScannedTwice()
    {
        var registration = await SeedRegistrationAsync();

        var first = await CheckInAsync(registration.EventId, registration.QrCode);
        var second = await CheckInAsync(registration.EventId, registration.QrCode);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await ReadJsonAsync(first);
        var secondBody = await ReadJsonAsync(second);
        firstBody.GetProperty("alreadyCheckedIn").GetBoolean().ShouldBeFalse();
        secondBody.GetProperty("alreadyCheckedIn").GetBoolean().ShouldBeTrue();
        secondBody.GetProperty("checkedInAt").GetDateTimeOffset()
            .ShouldBe(firstBody.GetProperty("checkedInAt").GetDateTimeOffset());
        secondBody.GetRawText().ShouldNotContain(registration.Email, Case.Insensitive);
    }

    [Fact]
    public async Task CheckIn_ReturnsTheSpecFieldsAndNoEmail_WhenTheCodeIsOnThisEvent()
    {
        var registration = await SeedRegistrationAsync();

        var response = await CheckInAsync(registration.EventId, registration.QrCode);

        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldNotContain(registration.Email, Case.Insensitive);
        raw.ShouldNotContain("email", Case.Insensitive);
        var body = JsonDocument.Parse(raw).RootElement;
        body.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["registrationId", "firstName", "lastName", "company", "reference", "checkedInAt", "checkedInById", "alreadyCheckedIn"],
            ignoreOrder: true);
        body.GetProperty("registrationId").GetGuid().ShouldBe(registration.Id);
        body.GetProperty("firstName").GetString().ShouldBe("María");
        body.GetProperty("lastName").GetString().ShouldBe("García");
        body.GetProperty("company").GetString().ShouldBe("Viajes del Caribe SRL");
        body.GetProperty("reference").GetString().ShouldBe(registration.Reference);
    }

    [Fact]
    public async Task CheckIn_ReturnsALocationToTheUndo_WhenItIsTheFirstCheckIn()
    {
        var registration = await SeedRegistrationAsync();

        var response = await CheckInAsync(registration.EventId, registration.QrCode);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.OriginalString
            .ShouldBe($"/v1/convocatorias/events/{registration.EventId}/check-ins/{registration.Id}");
    }

    [Fact]
    public async Task CheckIn_StampsTheCallersSub_WhenItChecksIn()
    {
        var registration = await SeedRegistrationAsync();
        var staff = Guid.NewGuid();

        var response = await CheckInAsync(registration.EventId, registration.QrCode, sub: staff);

        (await ReadJsonAsync(response)).GetProperty("checkedInById").GetGuid().ShouldBe(staff);
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldBe(staff);
    }

    /// <summary>Dos terminales a la vez: un check-in, nunca dos y nunca un error (spec §3.2).</summary>
    [Fact]
    public async Task CheckIn_ReturnsOne201AndOne200AndStampsOnce_WhenTwoTerminalsCheckInAtOnce()
    {
        var registration = await SeedRegistrationAsync();

        var responses = await Task.WhenAll(
            CheckInAsync(registration.EventId, registration.QrCode, sub: Guid.NewGuid()),
            CheckInAsync(registration.EventId, registration.QrCode, sub: Guid.NewGuid()));

        responses.Select(r => r.StatusCode)
            .ShouldBe([HttpStatusCode.Created, HttpStatusCode.OK], ignoreOrder: true);
        var bodies = await Task.WhenAll(responses.Select(ReadJsonAsync));
        bodies.Select(b => b.GetProperty("checkedInById").GetGuid()).Distinct().ShouldHaveSingleItem()
            .ShouldBe((await ReadCheckedInByIdAsync(registration.Id))!.Value);
    }

    /// <summary>Otro evento no nombra a nadie: ni la persona, ni su referencia, ni su evento.</summary>
    [Fact]
    public async Task CheckIn_Returns409NamingNobody_WhenTheCodeBelongsToAnotherEvent()
    {
        var registration = await SeedRegistrationAsync();

        var response = await CheckInAsync(Guid.NewGuid(), registration.QrCode);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.GetProperty("code").GetString().ShouldBe("CHECK_IN_WRONG_EVENT");
        raw.ShouldNotContain("María");
        raw.ShouldNotContain("García");
        raw.ShouldNotContain(registration.Reference);
        raw.ShouldNotContain(registration.EventId.ToString());
        raw.ShouldNotContain(registration.Id.ToString());
        raw.ShouldNotContain(registration.Email, Case.Insensitive);
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task CheckIn_Returns404_WhenNoRegistrationHasTheCode()
    {
        var response = await CheckInAsync(Guid.NewGuid(), "https://portal.example/entrada/" + Guid.NewGuid().ToString("N"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().ShouldBe("CHECK_IN_CODE_NOT_FOUND");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckIn_Returns400_WhenTheCodeIsEmpty(string qrCode)
    {
        var response = await CheckInAsync(Guid.NewGuid(), qrCode);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CheckIn_Returns400_WhenTheCodeIsLongerThan256Characters()
    {
        var response = await CheckInAsync(Guid.NewGuid(), new string('a', 257));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CheckIn_Returns400_WhenTheBodyHasNoCode()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync(Route(Guid.NewGuid()), new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CheckIn_Returns400_WhenThereIsNoBody()
    {
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var response = await _factory.CreateClient().PostAsync(Route(Guid.NewGuid()), content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ByRegistration_Returns201Then200WithTheFirstTimeAndNoEmail_WhenTheSameIdIsSentTwice()
    {
        var registration = await SeedRegistrationAsync();

        var first = await ByRegistrationAsync(registration.EventId, registration.Id);
        var second = await ByRegistrationAsync(registration.EventId, registration.Id);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstRaw = await first.Content.ReadAsStringAsync();
        var secondRaw = await second.Content.ReadAsStringAsync();
        firstRaw.ShouldNotContain("email", Case.Insensitive);
        secondRaw.ShouldNotContain(registration.Email, Case.Insensitive);
        var firstBody = JsonDocument.Parse(firstRaw).RootElement;
        var secondBody = JsonDocument.Parse(secondRaw).RootElement;
        firstBody.GetProperty("registrationId").GetGuid().ShouldBe(registration.Id);
        firstBody.GetProperty("alreadyCheckedIn").GetBoolean().ShouldBeFalse();
        secondBody.GetProperty("alreadyCheckedIn").GetBoolean().ShouldBeTrue();
        secondBody.GetProperty("checkedInAt").GetDateTimeOffset()
            .ShouldBe(firstBody.GetProperty("checkedInAt").GetDateTimeOffset());
    }

    /// <summary>La ruta deja dónde se deshace, no una URL que cuelgue de <c>by-registration</c>.</summary>
    [Fact]
    public async Task ByRegistration_ReturnsALocationToTheUndo_WhenItIsTheFirstCheckIn()
    {
        var registration = await SeedRegistrationAsync();

        var response = await ByRegistrationAsync(registration.EventId, registration.Id);

        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.OriginalString
            .ShouldBe($"/v1/convocatorias/events/{registration.EventId}/check-ins/{registration.Id}");
    }

    /// <summary>Una tecla de una pantalla vieja o de otro evento no nombra a nadie, como un QR de otro evento.</summary>
    [Fact]
    public async Task ByRegistration_Returns409NamingNobody_WhenTheRegistrationIsOnAnotherEvent()
    {
        var registration = await SeedRegistrationAsync();

        var response = await ByRegistrationAsync(Guid.NewGuid(), registration.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(raw).RootElement.GetProperty("code").GetString().ShouldBe("CHECK_IN_WRONG_EVENT");
        raw.ShouldNotContain("María");
        raw.ShouldNotContain("García");
        raw.ShouldNotContain(registration.Reference);
        raw.ShouldNotContain(registration.EventId.ToString());
        raw.ShouldNotContain(registration.Email, Case.Insensitive);
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task ByRegistration_Returns404_WhenTheRegistrationDoesNotExist()
    {
        var response = await ByRegistrationAsync(Guid.NewGuid(), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().ShouldBe("REGISTRATION_NOT_FOUND");
    }

    [Fact]
    public async Task ByRegistration_Returns400_WhenTheIdIsMissingOrEmpty()
    {
        var missing = await Client(sub: null, roles: null)
            .PostAsJsonAsync($"{Route(Guid.NewGuid())}/by-registration", new { });
        var empty = await ByRegistrationAsync(Guid.NewGuid(), Guid.Empty);

        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ByRegistration_Returns403_WhenTheCallerLacksCheckInWrite()
    {
        var registration = await SeedRegistrationAsync();

        var response = await ByRegistrationAsync(
            registration.EventId, registration.Id, roles: AuthorizationPolicies.CheckInRead);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task UndoThenCheckIn_Returns204ThenAFresh201WithALaterTime_WhenTheRegistrationWasCheckedIn()
    {
        var registration = await SeedRegistrationAsync();
        var first = await ReadJsonAsync(await CheckInAsync(registration.EventId, registration.QrCode));

        var undo = await UndoAsync(registration.EventId, registration.Id);
        var again = await CheckInAsync(registration.EventId, registration.QrCode);

        undo.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.Created);
        var againBody = await ReadJsonAsync(again);
        againBody.GetProperty("alreadyCheckedIn").GetBoolean().ShouldBeFalse();
        againBody.GetProperty("checkedInAt").GetDateTimeOffset()
            .ShouldBeGreaterThan(first.GetProperty("checkedInAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Undo_Returns204_WhenThereIsNoCheckInToClear()
    {
        var registration = await SeedRegistrationAsync();

        var response = await UndoAsync(registration.EventId, registration.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Undo_Returns404AndKeepsTheCheckIn_WhenTheRegistrationIsOnAnotherEvent()
    {
        var registration = await SeedRegistrationAsync();
        await CheckInAsync(registration.EventId, registration.QrCode);

        var response = await UndoAsync(Guid.NewGuid(), registration.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().ShouldBe("REGISTRATION_NOT_FOUND");
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Undo_Returns404_WhenTheRegistrationDoesNotExist()
    {
        var response = await UndoAsync(Guid.NewGuid(), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().ShouldBe("REGISTRATION_NOT_FOUND");
    }

    [Fact]
    public async Task CheckIn_Returns403_WhenTheCallerLacksCheckInWrite()
    {
        var registration = await SeedRegistrationAsync();

        var response = await CheckInAsync(registration.EventId, registration.QrCode, roles: AuthorizationPolicies.ConvocatoriasRead);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Undo_Returns403_WhenTheCallerLacksCheckInWrite()
    {
        var registration = await SeedRegistrationAsync();
        await CheckInAsync(registration.EventId, registration.QrCode);

        var response = await UndoAsync(registration.EventId, registration.Id, roles: AuthorizationPolicies.ConvocatoriasRead);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task CheckIn_Returns201_WhenTheCallerIsDoorStaff()
    {
        var registration = await SeedRegistrationAsync();

        var response = await CheckInAsync(registration.EventId, registration.QrCode, roles: TestAuthHandler.DoorStaffRoles);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task ByRegistration_Returns201_WhenTheCallerIsDoorStaff()
    {
        var registration = await SeedRegistrationAsync();

        var response = await ByRegistrationAsync(registration.EventId, registration.Id, roles: TestAuthHandler.DoorStaffRoles);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Undo_Returns204AndClearsTheCheckIn_WhenTheCallerIsDoorStaff()
    {
        var registration = await SeedRegistrationAsync();
        await CheckInAsync(registration.EventId, registration.QrCode);

        var response = await UndoAsync(registration.EventId, registration.Id, roles: TestAuthHandler.DoorStaffRoles);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ReadCheckedInByIdAsync(registration.Id)).ShouldBeNull();
    }

    private static string Route(Guid eventId) => $"/v1/convocatorias/events/{eventId}/check-ins";

    private Task<HttpResponseMessage> CheckInAsync(Guid eventId, string qrCode, Guid? sub = null, string? roles = null) =>
        Client(sub, roles).PostAsJsonAsync(Route(eventId), new { qrCode });

    private Task<HttpResponseMessage> ByRegistrationAsync(Guid eventId, Guid registrationId, string? roles = null) =>
        Client(sub: null, roles).PostAsJsonAsync($"{Route(eventId)}/by-registration", new { registrationId });

    private Task<HttpResponseMessage> UndoAsync(Guid eventId, Guid registrationId, string? roles = null) =>
        Client(sub: null, roles).DeleteAsync($"{Route(eventId)}/{registrationId}");

    private HttpClient Client(Guid? sub, string? roles)
    {
        var client = _factory.CreateClient();
        if (sub is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub.Value.ToString());
        if (roles is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<Guid?> ReadCheckedInByIdAsync(Guid registrationId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        return await context.Database.SqlQuery<Guid?>(
                $"""SELECT checked_in_by_id AS "Value" FROM convocatorias.registration WHERE id = {registrationId}""")
            .SingleAsync();
    }

    private sealed record SeededRegistration(Guid Id, Guid EventId, string QrCode, string Reference, string Email);

    /// <summary>Una inscripción sin check-in, en un evento propio del test.</summary>
    private async Task<SeededRegistration> SeedRegistrationAsync()
    {
        var unique = Guid.NewGuid().ToString("N");
        var registration = new SeededRegistration(
            Guid.CreateVersion7(), Guid.NewGuid(), unique, "MITUR-2026-" + unique, $"{unique}@acme.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, first_name, last_name, email, email_normalized, company, reference, qr_code,
                 contact_details_differ, created_at, updated_at)
            VALUES ({registration.Id}, {registration.EventId}, 'María', 'García', {registration.Email},
                    {registration.Email}, 'Viajes del Caribe SRL', {registration.Reference}, {registration.QrCode},
                    false, now(), now())
            """);
        return registration;
    }
}
