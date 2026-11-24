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
/// La lista de acceso de la puerta (spec <c>09</c> §5.1, PGTI-128) y lo que los coordinadores ven de
/// ella en el seguimiento (§6). Cada test siembra sus inscripciones en un evento recién inventado.
/// </summary>
public sealed class DoorListEndpointsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public DoorListEndpointsTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task List_ReturnsTheSpecFieldsAndNoEmail_WhenTheEventHasRegistrations()
    {
        var eventId = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García");

        var response = await ListAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldNotContain(maria.Email, Case.Insensitive);
        raw.ShouldNotContain("email", Case.Insensitive);
        var body = JsonDocument.Parse(raw).RootElement;
        body.EnumerateObject().Select(p => p.Name)
            .ShouldBe(["totals", "items", "total", "page", "pageSize"], ignoreOrder: true);
        var item = body.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["registrationId", "firstName", "lastName", "company", "reference", "checkedInAt"],
            ignoreOrder: true);
        item.GetProperty("registrationId").GetGuid().ShouldBe(maria.Id);
        item.GetProperty("reference").GetString().ShouldBe(maria.Reference);
        item.GetProperty("checkedInAt").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("page").GetInt32().ShouldBe(1);
        body.GetProperty("pageSize").GetInt32().ShouldBe(50);
    }

    [Theory]
    [InlineData("garcia")]
    [InlineData("maría garcía")]
    [InlineData("GARCÍA marí")]
    public async Task List_ReturnsTheRegistration_WhenTheSearchIsASubstringOfItsNames(string search)
    {
        var eventId = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García");
        await SeedRegistrationAsync(eventId, "Luis", "Peña");

        var items = await ItemsAsync(eventId, $"search={Uri.EscapeDataString(search)}");

        items.ShouldHaveSingleItem().GetProperty("registrationId").GetGuid().ShouldBe(maria.Id);
    }

    [Fact]
    public async Task List_ReturnsTheRegistration_WhenTheSearchIsItsWholeEmailInAnyCase()
    {
        var eventId = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García");
        await SeedRegistrationAsync(eventId, "Luis", "Peña");

        var items = await ItemsAsync(eventId, $"search={Uri.EscapeDataString(" " + maria.Email.ToUpperInvariant() + " ")}");

        items.ShouldHaveSingleItem().GetProperty("registrationId").GetGuid().ShouldBe(maria.Id);
    }

    [Fact]
    public async Task List_ReturnsNothing_WhenTheSearchIsOnlyAFragmentOfAnEmail()
    {
        var eventId = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García");

        var items = await ItemsAsync(eventId, $"search={Uri.EscapeDataString(maria.Email[..^4])}");

        items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("?search=zzz")]
    [InlineData("?checkedIn=true")]
    [InlineData("?checkedIn=false&search=pe%C3%B1a")]
    public async Task List_ReturnsTheEventTotals_WhateverTheFilters(string query)
    {
        var eventId = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García");
        await SeedRegistrationAsync(eventId, "Luis", "Peña");
        await SeedRegistrationAsync(eventId, "Ana", "Gómez");
        await ByRegistrationAsync(eventId, maria.Id);

        var body = await ReadJsonAsync(await _factory.CreateClient().GetAsync(Route(eventId) + query));

        var totals = body.GetProperty("totals");
        totals.GetProperty("registered").GetInt32().ShouldBe(3);
        totals.GetProperty("checkedIn").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheCheckedIn_WhenCheckedInIsTrue()
    {
        var eventId = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García");
        await SeedRegistrationAsync(eventId, "Luis", "Peña");
        await ByRegistrationAsync(eventId, maria.Id);

        var items = await ItemsAsync(eventId, "checkedIn=true");

        var item = items.ShouldHaveSingleItem();
        item.GetProperty("registrationId").GetGuid().ShouldBe(maria.Id);
        item.GetProperty("checkedInAt").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task List_ReturnsClampedPaging_WhenPageAndPageSizeAreOutOfRange()
    {
        var eventId = Guid.NewGuid();
        await SeedRegistrationAsync(eventId, "María", "García");

        var response = await _factory.CreateClient().GetAsync(Route(eventId) + "?page=0&pageSize=500");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("page").GetInt32().ShouldBe(1);
        body.GetProperty("pageSize").GetInt32().ShouldBe(100);
    }

    [Fact]
    public async Task List_Returns403_WhenTheCallerLacksCheckInRead()
    {
        var response = await ListAsync(Guid.NewGuid(), roles: AuthorizationPolicies.ConvocatoriasRead);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <c>check-in:write</c> no alcanza para leer la lista: son dos roles, y un puesto que solo escanea
    /// no la ve.
    /// </summary>
    [Fact]
    public async Task List_Returns403_WhenTheCallerHoldsOnlyCheckInWrite()
    {
        var response = await ListAsync(Guid.NewGuid(), roles: AuthorizationPolicies.CheckInWrite);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_ReturnsTheRegistration_WhenTheCallerIsDoorStaff()
    {
        var eventId = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García");

        var response = await ListAsync(eventId, roles: TestAuthHandler.DoorStaffRoles);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("registrationId").GetGuid().ShouldBe(maria.Id);
    }

    /// <summary>
    /// Lo que ve el coordinador (§6) y lo que ve la puerta son el mismo número: el resumen cuenta las
    /// inscripciones con check-in y la fila lleva la hora.
    /// </summary>
    [Fact]
    public async Task Tracking_ReturnsTheDoorListsCheckedInCountAndTimes_WhenSomeoneCheckedIn()
    {
        var eventId = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García");
        var luis = await SeedRegistrationAsync(eventId, "Luis", "Peña");
        var checkIn = await ReadJsonAsync(await ByRegistrationAsync(eventId, maria.Id));
        var checkedInAt = checkIn.GetProperty("checkedInAt").GetDateTimeOffset();

        var door = await ReadJsonAsync(await ListAsync(eventId));
        var tracking = await ReadJsonAsync(await _factory.CreateClient().GetAsync($"/v1/convocatorias/events/{eventId}/tracking"));
        var participants = await ReadJsonAsync(await _factory.CreateClient().GetAsync($"/v1/convocatorias/events/{eventId}/participants"));

        tracking.GetProperty("registrations").GetProperty("checkedIn").GetInt32()
            .ShouldBe(door.GetProperty("totals").GetProperty("checkedIn").GetInt32());
        tracking.GetProperty("registrations").GetProperty("checkedIn").GetInt32().ShouldBe(1);
        var rows = participants.GetProperty("items").EnumerateArray()
            .ToDictionary(row => row.GetProperty("email").GetString()!);
        rows[maria.Email].GetProperty("checkedInAt").GetDateTimeOffset().ShouldBe(checkedInAt);
        rows[luis.Email].GetProperty("checkedInAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ParticipantsCsv_ReturnsTheCheckInColumn_WhenSomeoneCheckedIn()
    {
        var eventId = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García");
        await ByRegistrationAsync(eventId, maria.Id);

        var csv = await _factory.CreateClient().GetStringAsync($"/v1/convocatorias/events/{eventId}/participants.csv");

        var lines = csv.Split("\r\n");
        lines[0].ShouldEndWith(",Fecha de check-in");
        lines[1].ShouldStartWith(maria.Email);
        lines[1].ShouldEndWith("Z");
    }

    private static string Route(Guid eventId) => $"/v1/convocatorias/events/{eventId}/registrations/door";

    private Task<HttpResponseMessage> ListAsync(Guid eventId, string? roles = null)
    {
        var client = _factory.CreateClient();
        if (roles is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client.GetAsync(Route(eventId));
    }

    private async Task<List<JsonElement>> ItemsAsync(Guid eventId, string query)
    {
        var response = await _factory.CreateClient().GetAsync($"{Route(eventId)}?{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return [.. (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray()];
    }

    private Task<HttpResponseMessage> ByRegistrationAsync(Guid eventId, Guid registrationId) =>
        _factory.CreateClient().PostAsJsonAsync(
            $"/v1/convocatorias/events/{eventId}/check-ins/by-registration", new { registrationId });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private sealed record SeededRegistration(Guid Id, string Reference, string Email);

    /// <summary>Una inscripción sin check-in, con una dirección y una referencia únicas.</summary>
    private async Task<SeededRegistration> SeedRegistrationAsync(Guid eventId, string firstName, string lastName)
    {
        var unique = Guid.NewGuid().ToString("N");
        var registration = new SeededRegistration(Guid.CreateVersion7(), "MITUR-2026-" + unique, $"{unique}@acme.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, first_name, last_name, email, email_normalized, company, reference, qr_code,
                 contact_details_differ, created_at, updated_at)
            VALUES ({registration.Id}, {eventId}, {firstName}, {lastName}, {registration.Email},
                    {registration.Email}, 'Viajes del Caribe SRL', {registration.Reference}, {unique},
                    false, now(), now())
            """);
        return registration;
    }
}
