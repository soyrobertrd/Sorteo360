using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

public sealed class RaffleEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;
    private readonly FakeAttendanceSource _attendance;

    public RaffleEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _attendance = factory.Attendance;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    private static string RaffleUrl(Guid eventId) => RaffleTestApi.RaffleUrl(eventId);

    #region Create + Get

    [Fact]
    public async Task CreateRaffle_Returns201AndGetReturnsTheDefaults_WhenTheEventHasNone()
    {
        var eventId = await _api.CreateEventAsync();

        var create = await _client.PostAsync(RaffleUrl(eventId), content: null);
        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var raffleId = await create.Content.ReadFromJsonAsync<Guid>();
        create.Headers.Location!.ToString().ShouldBe($"/v1/events/{eventId}/raffle");

        var raffle = await _api.GetRaffleAsync(eventId);
        raffle.Id.ShouldBe(raffleId);
        raffle.EventId.ShouldBe(eventId);
        raffle.State.ShouldBe("draft");
        raffle.VisualStyle.ShouldBe("roulette");
        raffle.ShowPrizeOnScreen.ShouldBeTrue();
        raffle.AnimationSeconds.ShouldBe(6);
        raffle.RequiresPresenceConfirmation.ShouldBeFalse();
        raffle.OnePrizePerPerson.ShouldBeTrue();
        raffle.OnePrizePerCompany.ShouldBeFalse();
        raffle.NotaryName.ShouldBeNull();
        raffle.WitnessTwoIdentityDocument.ShouldBeNull();
        raffle.Rounds.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateRaffle_Returns409RaffleAlreadyExists_WhenTheEventHasOne()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var again = await _client.PostAsync(RaffleUrl(eventId), content: null);

        await RaffleTestApi.ShouldBeProblemAsync(again, HttpStatusCode.Conflict, "RAFFLE_ALREADY_EXISTS");
    }

    [Fact]
    public async Task CreateRaffle_Returns404EventNotFound_WhenTheEventDoesNotExist()
    {
        var create = await _client.PostAsync(RaffleUrl(Guid.NewGuid()), content: null);

        await RaffleTestApi.ShouldBeProblemAsync(create, HttpStatusCode.NotFound, "EVENT_NOT_FOUND");
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("cancelled")]
    public async Task CreateRaffle_Returns409EventNotEditable_WhenTheEventIsOver(string status)
    {
        var eventId = await _api.CreateEventAsync();
        await _api.SetEventStatusAsync(eventId, status);

        var create = await _client.PostAsync(RaffleUrl(eventId), content: null);

        await RaffleTestApi.ShouldBeProblemAsync(create, HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
    }

    [Fact]
    public async Task GetRaffle_Returns404RaffleNotFound_WhenTheEventHasNone()
    {
        var eventId = await _api.CreateEventAsync();

        var get = await _client.GetAsync(RaffleUrl(eventId));

        await RaffleTestApi.ShouldBeProblemAsync(get, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task GetRaffle_Returns404EventNotFound_WhenTheEventDoesNotExist()
    {
        var get = await _client.GetAsync(RaffleUrl(Guid.NewGuid()));

        await RaffleTestApi.ShouldBeProblemAsync(get, HttpStatusCode.NotFound, "EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task GetRaffle_Returns200_WhenTheEventIsClosed()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.SetEventStatusAsync(eventId, "closed");

        var get = await _client.GetAsync(RaffleUrl(eventId));

        get.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Con la puerta caída

    [Fact]
    public async Task GetAttendees_Returns200UnavailableWithNullCountsAndList_WhenTheSourceRefuses()
    {
        // Lo desconocido no es vacío: ni cero ni una lista vacía, sino nulos.
        var eventId = await _api.CreateEventWithRaffleAsync();
        _attendance.Fail(eventId);

        var response = await _client.GetAsync($"{RaffleUrl(eventId)}/attendees");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        root.GetProperty("attendanceStatus").GetString().ShouldBe("unavailable");
        root.GetProperty("attendanceFetchedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("checkedIn").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("qualified").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("attendees").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("awardsBatchId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetRaffle_ReturnsOnlyAttendanceUnavailableAndTheAwardsWarning_WhenTheSourceRefusesAndNoAwardsListIsInForce()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _attendance.Fail(eventId);

        var raffle = await _api.GetRaffleAsync(eventId);

        raffle.IsReadyToStart.ShouldBe(false);
        raffle.BlockingReasons.ShouldBe(["attendance_unavailable"]);
        raffle.Warnings.ShouldContain("awards_list_not_in_force");
    }

    [Fact]
    public async Task SetOverride_Returns503AttendanceUnavailable_WhenTheSourceRefuses()
    {
        // Con la puerta caída una decisión manual es un 503, igual que arrancar: escribir valida en vivo.
        var eventId = await _api.CreateEventWithRaffleAsync();
        _attendance.Fail(eventId);

        var response = await _client.PostAsJsonAsync(
            $"{RaffleUrl(eventId)}/attendees/overrides", new { email = "ana@agencia.com", decision = "excluded" });

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "ATTENDANCE_UNAVAILABLE");
    }

    #endregion

    #region Update

    [Fact]
    public async Task UpdateRaffle_Returns204AndReplacesEveryField_WhenTheBodyIsComplete()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var patch = await _client.PatchAsJsonAsync(RaffleUrl(eventId), FullUpdate());
        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent, await patch.Content.ReadAsStringAsync());

        var raffle = await _api.GetRaffleAsync(eventId);
        raffle.VisualStyle.ShouldBe("sequential_names");
        raffle.ShowPrizeOnScreen.ShouldBeFalse();
        raffle.AnimationSeconds.ShouldBe(30);
        raffle.RequiresPresenceConfirmation.ShouldBeTrue();
        // Pedir que no, deja que sí: un premio por persona no se apaga.
        raffle.OnePrizePerPerson.ShouldBeTrue();
        raffle.OnePrizePerCompany.ShouldBeTrue();
        raffle.NotaryName.ShouldBe("Ana Pérez");
        raffle.NotaryIdentityDocument.ShouldBe("001-0000001-1");
        raffle.WitnessOneName.ShouldBe("Luis Gómez");
        raffle.WitnessOneIdentityDocument.ShouldBe("001-0000002-2");
        raffle.WitnessTwoName.ShouldBe("María Díaz");
        raffle.WitnessTwoIdentityDocument.ShouldBe("001-0000003-3");
    }

    [Fact]
    public async Task UpdateRaffle_Returns204ClearingTheAuthority_WhenItIsOmitted()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        (await _client.PatchAsJsonAsync(RaffleUrl(eventId), FullUpdate())).EnsureSuccessStatusCode();

        var patch = await _client.PatchAsJsonAsync(RaffleUrl(eventId), FullUpdate() with { NotaryName = null });
        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var raffle = await _api.GetRaffleAsync(eventId);
        raffle.NotaryName.ShouldBeNull();
        raffle.WitnessOneName.ShouldBe("Luis Gómez");
    }

    public static TheoryData<string> RequiredFields =>
    [
        nameof(UpdateRaffleRequest.VisualStyle),
        nameof(UpdateRaffleRequest.ShowPrizeOnScreen),
        nameof(UpdateRaffleRequest.AnimationSeconds),
        nameof(UpdateRaffleRequest.RequiresPresenceConfirmation),
        nameof(UpdateRaffleRequest.OnePrizePerPerson),
        nameof(UpdateRaffleRequest.OnePrizePerCompany),
    ];

    [Theory]
    [MemberData(nameof(RequiredFields))]
    public async Task UpdateRaffle_Returns400_WhenARequiredFieldIsOmitted(string field)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var request = field switch
        {
            nameof(UpdateRaffleRequest.VisualStyle) => FullUpdate() with { VisualStyle = null },
            nameof(UpdateRaffleRequest.ShowPrizeOnScreen) => FullUpdate() with { ShowPrizeOnScreen = null },
            nameof(UpdateRaffleRequest.AnimationSeconds) => FullUpdate() with { AnimationSeconds = null },
            nameof(UpdateRaffleRequest.RequiresPresenceConfirmation) => FullUpdate() with { RequiresPresenceConfirmation = null },
            nameof(UpdateRaffleRequest.OnePrizePerPerson) => FullUpdate() with { OnePrizePerPerson = null },
            _ => FullUpdate() with { OnePrizePerCompany = null },
        };

        var patch = await _client.PatchAsJsonAsync(RaffleUrl(eventId), request);

        patch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(31)]
    public async Task UpdateRaffle_Returns400_WhenTheAnimationLengthIsOutOfRange(int seconds)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var patch = await _client.PatchAsJsonAsync(RaffleUrl(eventId), FullUpdate() with { AnimationSeconds = seconds });

        patch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("tombola")]
    [InlineData("Roulette")]
    [InlineData("ruleta")]
    public async Task UpdateRaffle_Returns400_WhenTheVisualStyleIsNotACode(string visualStyle)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var patch = await _client.PatchAsJsonAsync(RaffleUrl(eventId), FullUpdate() with { VisualStyle = visualStyle });

        patch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRaffle_Returns400_WhenAnAuthorityIsTooLong()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var patch = await _client.PatchAsJsonAsync(
            RaffleUrl(eventId), FullUpdate() with { WitnessTwoIdentityDocument = new string('9', 101) });

        patch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRaffle_Returns404RaffleNotFound_WhenTheEventHasNone()
    {
        var eventId = await _api.CreateEventAsync();

        var patch = await _client.PatchAsJsonAsync(RaffleUrl(eventId), FullUpdate());

        await RaffleTestApi.ShouldBeProblemAsync(patch, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateRaffle_Returns404EventNotFound_WhenTheEventDoesNotExist()
    {
        var patch = await _client.PatchAsJsonAsync(RaffleUrl(Guid.NewGuid()), FullUpdate());

        await RaffleTestApi.ShouldBeProblemAsync(patch, HttpStatusCode.NotFound, "EVENT_NOT_FOUND");
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("cancelled")]
    public async Task UpdateRaffle_Returns409EventNotEditable_WhenTheEventIsOver(string status)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.SetEventStatusAsync(eventId, status);

        var patch = await _client.PatchAsJsonAsync(RaffleUrl(eventId), FullUpdate());

        await RaffleTestApi.ShouldBeProblemAsync(patch, HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
    }

    #endregion

    #region Authorization

    [Fact]
    public async Task CreateRaffle_Returns403_WhenTheCallerLacksRafflesWrite()
    {
        var eventId = await _api.CreateEventAsync();

        var create = await _api.SendAsync(HttpMethod.Post, RaffleUrl(eventId), content: null, roles: AuthorizationPolicies.RafflesDraw);

        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateRaffle_Returns403_WhenTheCallerLacksRafflesWrite()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var patch = await _api.SendAsync(
            HttpMethod.Patch, RaffleUrl(eventId), JsonContent.Create(FullUpdate()), roles: AuthorizationPolicies.RafflesDraw);

        patch.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRaffle_Returns200_WhenTheCallerHasNoRaffleRole()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var get = await _api.SendAsync(HttpMethod.Get, RaffleUrl(eventId), content: null, roles: $"{AuthorizationPolicies.EventsWrite},{AuthorizationPolicies.PlatformStaff}");

        get.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateRaffle_Returns201_WhenTheCallerHasOnlyRafflesWrite()
    {
        var eventId = await _api.CreateEventAsync();

        var create = await _api.SendAsync(HttpMethod.Post, RaffleUrl(eventId), content: null, roles: AuthorizationPolicies.RafflesWrite);

        create.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    #endregion

    private static UpdateRaffleRequest FullUpdate() => new()
    {
        VisualStyle = "sequential_names",
        ShowPrizeOnScreen = false,
        AnimationSeconds = 30,
        RequiresPresenceConfirmation = true,
        OnePrizePerPerson = false,
        OnePrizePerCompany = true,
        NotaryName = "Ana Pérez",
        NotaryIdentityDocument = "001-0000001-1",
        WitnessOneName = "Luis Gómez",
        WitnessOneIdentityDocument = "001-0000002-2",
        WitnessTwoName = "María Díaz",
        WitnessTwoIdentityDocument = "001-0000003-3",
    };
}
