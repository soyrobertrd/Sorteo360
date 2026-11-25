using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>POST …/attendees/overrides/bulk-exclude</c> contra el padrón real (<c>RosterAttendanceSource</c>):
/// los ganadores vivos de otro sorteo de la cuenta, o una lista de correos e IDs, salen del bombo
/// de una vez. Quien no está se informa; nada falla a medias.
/// </summary>
public sealed class RaffleBulkExcludeEndpointsTests : IClassFixture<EventsApiDbFactory>
{
    private readonly EventsApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleBulkExcludeEndpointsTests(EventsApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    private static readonly RaffleParticipantInputDto[] Trio =
    [
        new(FullName: "Ana Uno", Email: "ana@banco.example", EmployeeCode: "E-1", CheckedIn: true),
        new(FullName: "Beto Dos", Email: "beto@banco.example", EmployeeCode: "E-2", CheckedIn: true),
        new(FullName: "Carla Tres", Email: "carla@banco.example", EmployeeCode: "E-3", CheckedIn: true),
    ];

    [Fact]
    public async Task BulkExclude_ExcludesTheLiveWinnersOfTheSource_AndIsIdempotent()
    {
        var (source, voidedKey) = await SourceWithTwoLiveWinnersAndOneVoidedAsync(Trio);
        var target = await DraftWithRosterAsync(Trio);

        var first = await BulkExcludeAsync(target, new { sourceEventId = source });
        var again = await BulkExcludeAsync(target, new { sourceEventId = source });

        first.Excluded.ShouldBe(2);
        first.AlreadyExcluded.ShouldBe(0);
        first.NotInRoster.ShouldBeEmpty();
        again.Excluded.ShouldBe(0);
        again.AlreadyExcluded.ShouldBe(2);
        var attendees = (await _api.GetAttendeesAsync(target)).Attendees.ShouldNotBeNull();
        attendees.Where(a => a.ExclusionReason == ExclusionReason.ManuallyExcluded).Select(a => a.IdentityKey)
            .ShouldBe(Trio.Select(p => p.Email!).Where(e => e != voidedKey), ignoreOrder: true);
        attendees.Single(a => a.IdentityKey == voidedKey).Qualified.ShouldBeTrue();
        (await _api.GetStoredOverridesAsync(target)).ShouldAllBe(o => o.Note!.StartsWith("Ganador de «"));
    }

    [Fact]
    public async Task BulkExclude_ReportsAWinnerWhoIsNotAtTheTargetDoor()
    {
        var (source, _) = await SourceWithTwoLiveWinnersAndOneVoidedAsync(Trio);
        var target = await DraftWithRosterAsync(Trio[0] with { CheckedIn = false }, Trio[1], Trio[2]);

        var outcome = await BulkExcludeAsync(target, new { sourceEventId = source });

        (outcome.Excluded + outcome.NotInRoster.Count).ShouldBe(2);
        outcome.NotInRoster.ShouldAllBe(label => label == "ana@banco.example");
    }

    [Fact]
    public async Task BulkExclude_ClassifiesMixedIdentifiers()
    {
        var target = await DraftWithRosterAsync(Trio);

        var outcome = await BulkExcludeAsync(
            target, new { identifiers = new[] { "  ANA@Banco.example ", "e-2", "x@y.example", "basura", "", "E-2" } });

        outcome.Excluded.ShouldBe(2);
        outcome.NotInRoster.ShouldBe(["x@y.example"]);
        outcome.Invalid.ShouldBe(["basura"]);
        outcome.Unidentifiable.ShouldBeEmpty();
        (await _api.GetStoredOverridesAsync(target)).ShouldAllBe(o => o.Note == "Importado en lote");
    }

    [Fact]
    public async Task BulkExclude_TurnsAnInclusionIntoAnExclusion()
    {
        var target = await DraftWithRosterAsync(Trio);
        (await _api.SetOverrideAsync(target, "ana@banco.example", AttendeeOverrideDecision.Included))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var outcome = await BulkExcludeAsync(target, new { identifiers = new[] { "ana@banco.example" }, note = "Ya ganó" });

        outcome.Excluded.ShouldBe(1);
        (await _api.GetStoredOverridesAsync(target)).ShouldHaveSingleItem().Decision.ShouldBe(AttendeeOverrideDecision.Excluded);
    }

    [Fact]
    public async Task BulkExclude_Returns409AndWritesNothing_WhenTheTargetStarted()
    {
        var target = await DraftWithRosterAsync(Trio);
        await _api.CreatePrizesAsync(target, await _api.CreateRoundAsync(target));
        (await _api.StartAsync(target)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await PostAsync(target, new { identifiers = new[] { "ana@banco.example" } });

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
        (await _api.GetStoredOverridesAsync(target)).ShouldBeEmpty();
    }

    [Fact]
    public async Task BulkExclude_Returns400_WhenTheSourcesAreWrongOrTooMany()
    {
        var target = await DraftWithRosterAsync(Trio);

        await RaffleTestApi.ShouldBeProblemAsync(
            await PostAsync(target, new { }), HttpStatusCode.BadRequest, "RAFFLE_BULK_EXCLUDE_SOURCE_REQUIRED");
        await RaffleTestApi.ShouldBeProblemAsync(
            await PostAsync(target, new { sourceEventId = Guid.NewGuid(), identifiers = new[] { "a@b.example" } }),
            HttpStatusCode.BadRequest, "RAFFLE_BULK_EXCLUDE_SOURCE_REQUIRED");
        await RaffleTestApi.ShouldBeProblemAsync(
            await PostAsync(target, new { sourceEventId = target }), HttpStatusCode.BadRequest, "RAFFLE_BULK_EXCLUDE_SAME_EVENT");
        await RaffleTestApi.ShouldBeProblemAsync(
            await PostAsync(target, new { identifiers = Enumerable.Range(0, 5001).Select(i => $"E-{i}").ToArray() }),
            HttpStatusCode.BadRequest, "RAFFLE_BULK_EXCLUDE_TOO_MANY");
    }

    [Fact]
    public async Task BulkExclude_Returns404_WhenTheSourceIsOfAnotherAccount()
    {
        var target = await DraftWithRosterAsync(Trio);
        var otherAccount = await SeedAccountAsync("Cuenta B exclusión");
        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add(TestAuthHandler.AccountIdHeader, otherAccount.ToString("D"));
        var sourceB = await new RaffleTestApi(_factory, clientB).CreateEventWithRaffleAsync();

        var response = await PostAsync(target, new { sourceEventId = sourceB });

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task BulkExclude_RecognizesAWinnerWithoutEmailByHerId()
    {
        RaffleParticipantInputDto pedro = new(FullName: "Pedro Sin Correo", EmployeeCode: "E-55", CheckedIn: true);
        var source = await DraftWithRosterAsync(pedro);
        var prize = (await _api.CreatePrizesAsync(source, await _api.CreateRoundAsync(source)))[0].Id;
        (await _api.StartAsync(source)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.DrawAsync(source, prize)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var target = await DraftWithRosterAsync(pedro, Trio[0]);

        var outcome = await BulkExcludeAsync(target, new { sourceEventId = source });

        outcome.Excluded.ShouldBe(1);
        (await _api.GetStoredOverridesAsync(target)).ShouldHaveSingleItem().Email.ShouldBe("id:e-55");
    }

    [Fact]
    public async Task BulkExclude_ReportsAWinnerWithNeitherEmailNorIdByName_AndExcludesNobodyForHer()
    {
        var source = await DraftWithRosterAsync(Trio[0]);
        var prize = (await _api.CreatePrizesAsync(source, await _api.CreateRoundAsync(source)))[0].Id;
        (await _api.StartAsync(source)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.DrawAsync(source, prize)).StatusCode.ShouldBe(HttpStatusCode.OK);
        // Una fila vieja sin correo ni ID, como las que dejó la migración: legacy:{id}.
        await ExecuteSqlAsync(
            $"UPDATE events.prize_draw SET winner_identity_key = 'legacy:' || winner_attendee_id::text WHERE prize_id = {prize}");
        var target = await DraftWithRosterAsync(Trio);

        var outcome = await BulkExcludeAsync(target, new { sourceEventId = source });

        outcome.Excluded.ShouldBe(0);
        outcome.Unidentifiable.ShouldBe(["Ana Uno"]);
        (await _api.GetStoredOverridesAsync(target)).ShouldBeEmpty();
    }

    [Fact]
    public async Task BulkExclude_ExcludesAPersonWithoutEmailFromAnImportedId()
    {
        var target = await DraftWithRosterAsync(new RaffleParticipantInputDto(FullName: "Pedro Sin Correo", EmployeeCode: "E-55", CheckedIn: true));

        var outcome = await BulkExcludeAsync(target, new { identifiers = new[] { "E-55" } });

        outcome.Excluded.ShouldBe(1);
        (await _api.GetAttendeesAsync(target)).Attendees.ShouldNotBeNull().ShouldHaveSingleItem()
            .ExclusionReason.ShouldBe(ExclusionReason.ManuallyExcluded);
    }

    /// <summary>Un sorteo con los tres sorteados y el primero anulado: quedan dos ganadores vivos.</summary>
    private async Task<(Guid EventId, string VoidedKey)> SourceWithTwoLiveWinnersAndOneVoidedAsync(
        params RaffleParticipantInputDto[] people)
    {
        var eventId = await DraftWithRosterAsync(people);
        var prizes = await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), quantity: 3);
        (await _api.StartAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var prize in prizes)
            (await _api.DrawAsync(eventId, prize.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.VoidAsync(eventId, prizes[0].Id, PrizeDrawVoidCode.Declined)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var voided = (await _api.GetStoredDrawsAsync(eventId)).Single(d => d.PrizeId == prizes[0].Id);
        return (eventId, voided.WinnerIdentityKey);
    }

    private async Task<Guid> DraftWithRosterAsync(params RaffleParticipantInputDto[] people)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var import = await _client.PostAsJsonAsync(
            $"{RaffleTestApi.RaffleUrl(eventId)}/participants/import",
            new ImportRaffleParticipantsHttpRequest(people, ReplaceExisting: true, Source: "excel"));
        import.StatusCode.ShouldBe(HttpStatusCode.OK, await import.Content.ReadAsStringAsync());
        return eventId;
    }

    private Task<HttpResponseMessage> PostAsync(Guid eventId, object body) =>
        _client.PostAsJsonAsync($"{RaffleTestApi.OverridesUrl(eventId)}/bulk-exclude", body);

    private async Task<BulkExcludeAttendeesResponse> BulkExcludeAsync(Guid eventId, object body)
    {
        var response = await PostAsync(eventId, body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BulkExcludeAttendeesResponse>())!;
    }

    private async Task ExecuteSqlAsync(FormattableString sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(sql);
    }

    private async Task<Guid> SeedAccountAsync(string name)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        db.Accounts.Add(new AccountData
        {
            Id = id,
            Name = name,
            Plan = PlanCatalog.Pro,
            Status = AccountStatus.Active,
            PlanStartedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return id;
    }
}
