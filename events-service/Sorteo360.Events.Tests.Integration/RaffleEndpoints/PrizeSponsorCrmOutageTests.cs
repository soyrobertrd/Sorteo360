using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// El patrocinador con el CRM caído: la escritura se rechaza sin escribir nada, y la lectura contesta
/// igual, con el nombre que no se pudo resolver en <c>null</c> y la cabecera degradada. Clase propia
/// porque tumba al CRM, y el circuito del chasis es uno por destino.
/// </summary>
public sealed class PrizeSponsorCrmOutageTests : IClassFixture<RaffleCompletionApiDbFactory>
{
    private const string SponsorName = "Brugal & Co.";

    private readonly RaffleCompletionApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public PrizeSponsorCrmOutageTests(RaffleCompletionApiDbFactory factory)
    {
        _factory = factory;
        _factory.Realm.ServeDefaults();
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task CreatePrizes_Returns503AndWritesNothing_WhenTheCrmDoesNotAnswer()
    {
        var companyId = Guid.NewGuid();
        _factory.Crm.ServeCompanies(new StubCrmCompany(companyId, SponsorName, IsActive: true, IsDeleted: false));
        var eventId = await _api.CreateEventWithRaffleAsync();
        var roundId = await _api.CreateRoundAsync(eventId);
        _factory.Crm.CompaniesGoDown();

        var response = await _client.PostAsJsonAsync(PrizesUrl(eventId), new CreatePrizesRequest
        {
            RoundId = roundId, Name = "Ron", Quantity = 1, SponsorCompanyId = companyId,
        });

        await ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "CRM_UNAVAILABLE");
        _factory.Crm.ServeCompanies(new StubCrmCompany(companyId, SponsorName, IsActive: true, IsDeleted: false));
        (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRaffle_Returns200WithANullContactNameAndAStaleHeader_WhenTheContactsDoNotAnswer()
    {
        var companyId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        _factory.Crm.ServeCompanies(new StubCrmCompany(companyId, SponsorName, IsActive: true, IsDeleted: false));
        _factory.Crm.ServeContacts(companyId, new StubCrmContact(contactId, "Laura Brugal", null, null));
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), "Ron", 1, companyId, contactId);
        _factory.Crm.ContactsGoDown();

        var response = await _client.GetAsync(RaffleUrl(eventId));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.GetValues("X-Crm-Directory-Status").Single().ShouldBe("stale");
        var sponsor = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!.Rounds.Single().Prizes.Single().Sponsor!;
        sponsor.CompanyName.ShouldBe(SponsorName);
        sponsor.ContactId.ShouldBe(contactId);
        sponsor.ContactName.ShouldBeNull();
    }
}
