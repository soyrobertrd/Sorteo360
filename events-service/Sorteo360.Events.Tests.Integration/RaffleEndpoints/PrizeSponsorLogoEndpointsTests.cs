using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// El logo del patrocinador: es del premio, uno por premio, y una subida nueva reemplaza a la anterior
/// como la portada de un evento (spec 00 §8). Exige patrocinador, queda fijo con el premio una vez
/// sorteado, y cambiar o quitar la empresa lo desenlaza en la misma escritura.
/// </summary>
public sealed class PrizeSponsorLogoEndpointsTests : IClassFixture<RaffleCompletionApiDbFactory>
{
    private readonly RaffleCompletionApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public PrizeSponsorLogoEndpointsTests(RaffleCompletionApiDbFactory factory)
    {
        _factory = factory;
        _factory.Realm.ServeDefaults();
        _factory.Crm.ServeAnyCompany();
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task UploadLogo_Returns201AndTheRaffleShowsIt_WhenThePrizeIsSponsored()
    {
        var (eventId, prizeId) = await SponsoredPrizeAsync();
        var bytes = SampleFiles.Png();

        var response = await _client.PostAsync(LogoUrl(eventId, prizeId), LogoForm(bytes, "brugal.png"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var uploaded = (await response.Content.ReadFromJsonAsync<PrizeSponsorLogoResponse>())!;
        uploaded.FileName.ShouldBe("brugal.png");
        uploaded.Missing.ShouldBeFalse();

        var logo = (await SponsorOfAsync(eventId)).Logo!;
        logo.FileId.ShouldBe(uploaded.FileId);
        logo.FileName.ShouldBe("brugal.png");
        logo.Missing.ShouldBeFalse();

        var content = await _client.GetAsync($"v1/files/{uploaded.FileId}/content");
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await content.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
    }

    [Fact]
    public async Task UploadLogo_ReplacesTheOldOneAndKeepsItsFile_WhenThePrizeAlreadyHasALogo()
    {
        var (eventId, prizeId) = await SponsoredPrizeAsync();
        var first = await UploadAsync(eventId, prizeId, "viejo.png");

        var second = await UploadAsync(eventId, prizeId, "nuevo.jpg", SampleFiles.Jpeg(), "image/jpeg");

        (await SponsorOfAsync(eventId)).Logo!.FileId.ShouldBe(second);
        (await _client.GetAsync($"v1/files/{first}/content")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveLogo_Returns204UnlinkingItAndKeepingItsFile_WhenThePrizeHasALogo()
    {
        var (eventId, prizeId) = await SponsoredPrizeAsync();
        var fileId = await UploadAsync(eventId, prizeId);

        var response = await _client.DeleteAsync(LogoUrl(eventId, prizeId));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await SponsorOfAsync(eventId)).Logo.ShouldBeNull();
        (await _client.GetAsync($"v1/files/{fileId}/content")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UploadLogo_Returns409PrizeNotSponsoredWritingNothing_WhenThePrizeHasNoSponsor()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var prizeId = (await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId))).Single().Id;
        var rows = await _api.CountStoredFileRowsAsync();
        var bytes = _api.CountStoredBytes();

        var response = await _client.PostAsync(LogoUrl(eventId, prizeId), LogoForm());

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "PRIZE_NOT_SPONSORED");
        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
        _api.CountStoredBytes().ShouldBe(bytes);
    }

    [Fact]
    public async Task UploadLogo_Returns400FileTypeNotAllowed_WhenTheFileIsAPdf()
    {
        var (eventId, prizeId) = await SponsoredPrizeAsync();
        var rows = await _api.CountStoredFileRowsAsync();

        var response = await _client.PostAsync(
            LogoUrl(eventId, prizeId), LogoForm(SampleFiles.Pdf(), "logo.pdf", "application/pdf"));

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "FILE_TYPE_NOT_ALLOWED");
        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
    }

    [Fact]
    public async Task UploadLogo_Returns404PrizeNotFound_WhenThePrizeIsNotOfThisRaffle()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _client.PostAsync(LogoUrl(eventId, Guid.NewGuid()), LogoForm());

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "PRIZE_NOT_FOUND");
    }

    [Fact]
    public async Task UploadLogo_Returns503FileStoreUnavailableWritingNothing_WhenTheStoreIsDown()
    {
        var (eventId, prizeId) = await SponsoredPrizeAsync();
        var rows = await _api.CountStoredFileRowsAsync();

        HttpResponseMessage response;
        using (_factory.Failing(FileStoreFault.Unavailable))
            response = await _client.PostAsync(LogoUrl(eventId, prizeId), LogoForm());

        await ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "FILE_STORE_UNAVAILABLE");
        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
        (await SponsorOfAsync(eventId)).Logo.ShouldBeNull();
    }

    [Fact]
    public async Task UploadAndRemoveLogo_Return409PrizeHasDraws_WhenThePrizeWasDrawn()
    {
        var (eventId, prizeId) = await SponsoredPrizeAsync();
        var fileId = await UploadAsync(eventId, prizeId);
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(2));
        (await _api.StartAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var draw = await _api.DrawAsync(eventId, prizeId);
        draw.StatusCode.ShouldBe(HttpStatusCode.OK, await draw.Content.ReadAsStringAsync());
        var rows = await _api.CountStoredFileRowsAsync();

        var upload = await _client.PostAsync(LogoUrl(eventId, prizeId), LogoForm());
        var remove = await _client.DeleteAsync(LogoUrl(eventId, prizeId));

        await ShouldBeProblemAsync(upload, HttpStatusCode.Conflict, "PRIZE_HAS_DRAWS");
        await ShouldBeProblemAsync(remove, HttpStatusCode.Conflict, "PRIZE_HAS_DRAWS");
        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
        // La ruta del sorteo devuelve un solo premio, y también trae su logo.
        (await draw.Content.ReadFromJsonAsync<PrizeResponse>())!.Sponsor!.Logo!.FileId.ShouldBe(fileId);
        (await SponsorOfAsync(eventId)).Logo!.FileId.ShouldBe(fileId);
    }

    [Fact]
    public async Task UpdatePrize_UnlinksTheLogo_WhenTheSponsorIsCleared()
    {
        var (eventId, prizeId) = await SponsoredPrizeAsync();
        await UploadAsync(eventId, prizeId);
        var roundId = (await _api.GetRaffleAsync(eventId)).Rounds.Single().Id;

        var patch = await _client.PatchAsJsonAsync(
            PrizeUrl(eventId, prizeId), new UpdatePrizeRequest { RoundId = roundId, Name = "Ron" });

        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent, await patch.Content.ReadAsStringAsync());
        (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.Single().Sponsor.ShouldBeNull();

        // Sin el logo viejo enganchado: volver a patrocinarlo no lo trae de vuelta.
        await PatchSponsorAsync(eventId, prizeId, roundId, Guid.NewGuid());
        (await SponsorOfAsync(eventId)).Logo.ShouldBeNull();
    }

    [Fact]
    public async Task UpdatePrize_UnlinksTheLogo_WhenTheSponsorCompanyChanges()
    {
        var (eventId, prizeId) = await SponsoredPrizeAsync();
        await UploadAsync(eventId, prizeId);
        var roundId = (await _api.GetRaffleAsync(eventId)).Rounds.Single().Id;
        var otherCompany = Guid.NewGuid();

        await PatchSponsorAsync(eventId, prizeId, roundId, otherCompany);

        var sponsor = await SponsorOfAsync(eventId);
        sponsor.CompanyId.ShouldBe(otherCompany);
        sponsor.Logo.ShouldBeNull();
    }

    [Fact]
    public async Task UpdatePrize_KeepsTheLogo_WhenTheSponsorCompanyStaysTheSame()
    {
        var (eventId, prizeId) = await SponsoredPrizeAsync();
        var fileId = await UploadAsync(eventId, prizeId);
        var raffle = await _api.GetRaffleAsync(eventId);
        var companyId = raffle.Rounds.Single().Prizes.Single().Sponsor!.CompanyId!.Value;

        await PatchSponsorAsync(eventId, prizeId, raffle.Rounds.Single().Id, companyId, name: "Ron añejo");

        (await SponsorOfAsync(eventId)).Logo!.FileId.ShouldBe(fileId);
    }

    private static string LogoUrl(Guid eventId, Guid prizeId) => $"{PrizeUrl(eventId, prizeId)}/logo";

    private static MultipartFormDataContent LogoForm(
        byte[]? bytes = null, string fileName = "logo.png", string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes ?? SampleFiles.Png());
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    /// <summary>Un sorteo en draft con un solo premio patrocinado por una empresa nueva.</summary>
    private async Task<(Guid EventId, Guid PrizeId)> SponsoredPrizeAsync()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var prizes = await _api.CreatePrizesAsync(
            eventId, await _api.CreateRoundAsync(eventId), "Ron", sponsorCompanyId: Guid.NewGuid());
        return (eventId, prizes.Single().Id);
    }

    private async Task<Guid> UploadAsync(
        Guid eventId, Guid prizeId, string fileName = "logo.png", byte[]? bytes = null, string contentType = "image/png")
    {
        var response = await _client.PostAsync(LogoUrl(eventId, prizeId), LogoForm(bytes, fileName, contentType));
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PrizeSponsorLogoResponse>())!.FileId;
    }

    private async Task PatchSponsorAsync(Guid eventId, Guid prizeId, Guid roundId, Guid companyId, string name = "Ron")
    {
        var patch = await _client.PatchAsJsonAsync(PrizeUrl(eventId, prizeId), new UpdatePrizeRequest
        {
            RoundId = roundId, Name = name, SponsorCompanyId = companyId,
        });
        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent, await patch.Content.ReadAsStringAsync());
    }

    private async Task<PrizeSponsorResponse> SponsorOfAsync(Guid eventId) =>
        (await _api.GetRaffleAsync(eventId)).Rounds.Single().Prizes.Single().Sponsor!;
}
