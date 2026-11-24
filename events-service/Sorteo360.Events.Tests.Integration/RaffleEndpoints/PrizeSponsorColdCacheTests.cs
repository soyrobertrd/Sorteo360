using System.Net;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// El acta imprime al patrocinador, así que un premio patrocinado cuya empresa no se puede nombrar
/// de ninguna forma —sin copia del directorio y con el CRM caído— frena la generación en lugar de
/// imprimirlo en blanco (spec 03 §4.2 punto 9). Clase propia porque necesita la caché en frío.
/// </summary>
public sealed class PrizeSponsorColdCacheTests : IClassFixture<RaffleCompletionApiDbFactory>
{
    private readonly RaffleCompletionApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public PrizeSponsorColdCacheTests(RaffleCompletionApiDbFactory factory)
    {
        _factory = factory;
        _factory.Realm.ServeDefaults();
        _api = new RaffleTestApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task Complete_Returns503AndWritesNoRecord_WhenTheSponsorCannotBeNamedAtAll()
    {
        var (eventId, _) = await _api.ReadyToCompleteAsync(_factory.Attendance);

        // El premio patrocinado entra con el sorteo en curso, sin que nada lea el sorteo después: la
        // copia del directorio nunca se llenó con esa empresa.
        var roundId = await _api.CreateRoundAsync(eventId, "Sorpresas");
        await _api.CreatePrizesAsync(eventId, roundId, "Ron", 1, sponsorCompanyId: Guid.NewGuid());
        _factory.Crm.CompaniesGoDown();

        var response = await _api.CompleteAsync(eventId);

        await ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "CRM_UNAVAILABLE");
        (await _api.GetStoredNotarialRecordsAsync(eventId)).ShouldBeEmpty();
    }
}
