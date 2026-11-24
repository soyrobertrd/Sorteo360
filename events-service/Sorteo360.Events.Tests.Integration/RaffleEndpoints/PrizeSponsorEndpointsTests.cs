using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// El patrocinador de un premio: una empresa del CRM y, si se quiere, uno de sus contactos, guardados
/// como ids y nombrados al leer (spec 00 §8, ADR-0013). Las escrituras validan en vivo contra el CRM y
/// fallan cerradas; las lecturas nunca fallan por un nombre.
/// </summary>
public sealed class PrizeSponsorEndpointsTests : IClassFixture<RaffleCompletionApiDbFactory>
{
    private const string SponsorName = "Brugal & Co.";
    private const string SponsorContactName = "Laura Brugal";

    private readonly RaffleCompletionApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public PrizeSponsorEndpointsTests(RaffleCompletionApiDbFactory factory)
    {
        _factory = factory;
        _factory.Realm.ServeDefaults();
        _factory.Crm.ServeAnyCompany();
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    #region Las escrituras

    [Fact]
    public async Task CreatePrizes_Returns201StampingTheSponsorOnEveryRow_WhenQuantityIsThree()
    {
        var (companyId, contactId) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();

        var created = await _api.CreatePrizesAsync(
            eventId, await _api.CreateRoundAsync(eventId), "Ron", quantity: 3, companyId, contactId);

        created.Count.ShouldBe(3);
        created.ShouldAllBe(p => p.Sponsor!.CompanyId == companyId && p.Sponsor.ContactId == contactId);
    }

    [Fact]
    public async Task CreatePrizes_Returns201WithoutASponsor_WhenNoneIsSent()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var created = await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId));

        created.Single().Sponsor.ShouldBeNull();
    }

    [Fact]
    public async Task CreatePrizes_Returns400_WhenAContactComesWithoutACompany()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _client.PostAsJsonAsync(PrizesUrl(eventId), new CreatePrizesRequest
        {
            RoundId = await _api.CreateRoundAsync(eventId),
            Name = "Ron",
            Quantity = 1,
            SponsorContactId = Guid.NewGuid(),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePrizes_Returns409CompanyNotAvailable_WhenTheCrmDoesNotKnowTheCompany()
    {
        _factory.Crm.ServeCompanies(Live(Guid.NewGuid()));
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _client.PostAsJsonAsync(
            PrizesUrl(eventId), Sponsored(await _api.CreateRoundAsync(eventId), Guid.NewGuid()));

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "COMPANY_NOT_AVAILABLE");
        (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreatePrizes_Returns201_WhenTheCompanyIsClosed()
    {
        // La misma regla que el proveedor: solo se rechaza un id que nunca existió.
        var companyId = Guid.NewGuid();
        _factory.Crm.ServeCompanies(new StubCrmCompany(companyId, SponsorName, IsActive: false, IsDeleted: true));
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _client.PostAsJsonAsync(
            PrizesUrl(eventId), Sponsored(await _api.CreateRoundAsync(eventId), companyId));

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreatePrizes_Returns409CompanyContactNotAvailable_WhenTheContactIsNotWithThatCompany()
    {
        var (companyId, _) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _client.PostAsJsonAsync(
            PrizesUrl(eventId), Sponsored(await _api.CreateRoundAsync(eventId), companyId, Guid.NewGuid()));

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "COMPANY_CONTACT_NOT_AVAILABLE");
    }

    [Fact]
    public async Task UpdatePrize_Returns204SettingTheSponsor_WhenTheCompanyAndContactAreValid()
    {
        var (companyId, contactId) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var prize = (await _api.CreatePrizesAsync(eventId, roundId)).Single();

        var patch = await _client.PatchAsJsonAsync(PrizeUrl(eventId, prize.Id), new UpdatePrizeRequest
        {
            RoundId = roundId, Name = "Ron", SponsorCompanyId = companyId, SponsorContactId = contactId,
        });

        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent, await patch.Content.ReadAsStringAsync());
        var sponsor = (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.Single().Sponsor!;
        sponsor.CompanyId.ShouldBe(companyId);
        sponsor.ContactId.ShouldBe(contactId);
    }

    [Fact]
    public async Task UpdatePrize_Returns204ClearingTheSponsor_WhenItIsOmitted()
    {
        var (companyId, contactId) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var prize = (await _api.CreatePrizesAsync(eventId, roundId, "Ron", 1, companyId, contactId)).Single();

        var patch = await _client.PatchAsJsonAsync(
            PrizeUrl(eventId, prize.Id), new UpdatePrizeRequest { RoundId = roundId, Name = "Ron" });

        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.Single().Sponsor.ShouldBeNull();
    }

    [Fact]
    public async Task UpdatePrize_Returns409CompanyContactNotAvailable_WhenTheContactIsNotWithThatCompany()
    {
        var (companyId, _) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        var prize = (await _api.CreatePrizesAsync(eventId, roundId)).Single();

        var patch = await _client.PatchAsJsonAsync(PrizeUrl(eventId, prize.Id), new UpdatePrizeRequest
        {
            RoundId = roundId, Name = "Ron", SponsorCompanyId = companyId, SponsorContactId = Guid.NewGuid(),
        });

        await ShouldBeProblemAsync(patch, HttpStatusCode.Conflict, "COMPANY_CONTACT_NOT_AVAILABLE");
        (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.Single().Sponsor.ShouldBeNull();
    }

    #endregion

    #region Las lecturas

    [Fact]
    public async Task GetRaffle_ResolvesTheSponsorsNamesWithStatusOk_WhenTheCrmAnswers()
    {
        var (companyId, contactId) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), "Ron", 1, companyId, contactId);

        var response = await _client.GetAsync(RaffleUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("X-Crm-Directory-Status").Single().ShouldBe("ok");
        var sponsor = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!.Rounds.Single().Prizes.Single().Sponsor!;
        sponsor.CompanyName.ShouldBe(SponsorName);
        sponsor.ContactName.ShouldBe(SponsorContactName);
    }

    [Fact]
    public async Task GetRaffle_AsksTheCrmForNoContact_WhenNoPrizeHasASponsorContact()
    {
        var (companyId, _) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), "Ron", 1, companyId);
        _factory.Crm.ResetRequestLog();

        var sponsor = (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.Single().Sponsor!;

        sponsor.CompanyName.ShouldBe(SponsorName);
        sponsor.ContactId.ShouldBeNull();
        sponsor.ContactName.ShouldBeNull();
        _factory.Crm.ContactsByIdRequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetRaffle_AsksForAllSponsorContactsInOneCall_WhenSeveralPrizesHaveOne()
    {
        var (companyId, contactId) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), "Ron", 4, companyId, contactId);
        _factory.Crm.ResetRequestLog();

        await _api.GetRaffleAsync(eventId);

        _factory.Crm.ContactsByIdRequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Start_ReturnsTheSponsorsNamesAndTheStatusHeader_WhenTheRaffleStarts()
    {
        var (companyId, contactId) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(2));
        await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), "Ron", 1, companyId, contactId);

        var response = await _api.StartAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.GetValues("X-Crm-Directory-Status").Single().ShouldBe("ok");
        var sponsor = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!.Rounds.Single().Prizes.Single().Sponsor!;
        sponsor.CompanyName.ShouldBe(SponsorName);
        sponsor.ContactName.ShouldBe(SponsorContactName);
    }

    [Fact]
    public async Task Complete_ReturnsTheSponsorsNamesAndTheStatusHeader_WhenTheRaffleCompletes()
    {
        var (companyId, _) = ServeSponsor();
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(2));
        await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), "Ron", 1, companyId);
        (await _api.StartAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());

        var response = await _api.CompleteAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.GetValues("X-Crm-Directory-Status").Single().ShouldBe("ok");
        (await response.Content.ReadFromJsonAsync<RaffleResponse>())!
            .Rounds.Single().Prizes.Single().Sponsor!.CompanyName.ShouldBe(SponsorName);
    }

    #endregion

    /// <summary>Una empresa patrocinadora con un contacto vigente, ambos con ids nuevos.</summary>
    private (Guid CompanyId, Guid ContactId) ServeSponsor()
    {
        var companyId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        _factory.Crm.ServeCompanies(Live(companyId));
        _factory.Crm.ServeContacts(companyId, new StubCrmContact(contactId, SponsorContactName, "laura@brugal.do", null));
        return (companyId, contactId);
    }

    private static StubCrmCompany Live(Guid id) => new(id, SponsorName, IsActive: true, IsDeleted: false);

    private static CreatePrizesRequest Sponsored(Guid roundId, Guid companyId, Guid? contactId = null) => new()
    {
        RoundId = roundId,
        Name = "Ron",
        Quantity = 1,
        SponsorCompanyId = companyId,
        SponsorContactId = contactId,
    };
}
