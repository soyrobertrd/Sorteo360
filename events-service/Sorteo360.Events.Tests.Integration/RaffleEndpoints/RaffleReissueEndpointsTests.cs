using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>POST …/raffle/reissue</c>: corregir las autoridades de un sorteo completado y escribir la
/// versión siguiente del acta (spec 03 §4.5). Las anteriores nunca se borran, y un intento fallido
/// deja vigente la que había.
/// </summary>
public sealed class RaffleReissueEndpointsTests : IClassFixture<RaffleCompletionApiDbFactory>
{
    private readonly RaffleCompletionApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleReissueEndpointsTests(RaffleCompletionApiDbFactory factory)
    {
        _factory = factory;
        _factory.Realm.ServeDefaults();
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    #region La reemisión

    [Fact]
    public async Task Reissue_Returns200WithTheNewVersionFirstAndTheCorrectedAuthorities_WhenCompleted()
    {
        var (eventId, completed) = await CompletedAsync();
        var first = completed.NotarialRecords.ShouldHaveSingleItem();

        var response = await _api.ReissueAsync(eventId, CorrectedAuthorities());

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
        body.State.ShouldBe("completed");
        body.CompletedAt.ShouldBe(completed.CompletedAt);
        body.CompletedById.ShouldBe(completed.CompletedById);
        body.WitnessTwoIdentityDocument.ShouldBe("001-0000030-3");
        body.NotarialRecords.Select(r => r.Version).ShouldBe([2, 1]);
        body.NotarialRecords[0].GeneratedById.ShouldBe(TestAuthHandler.DefaultUserId);
        body.NotarialRecords[0].StoredFileId.ShouldNotBe(first.StoredFileId);
        body.NotarialRecords[1].ShouldBe(first);

        var read = await _api.GetRaffleAsync(eventId);
        read.WitnessTwoIdentityDocument.ShouldBe("001-0000030-3");
        read.NotarialRecords.Select(r => r.Version).ShouldBe([2, 1]);
    }

    [Fact]
    public async Task Reissue_KeepsEveryVersionServable_WhenItSucceeds()
    {
        var (eventId, _) = await CompletedAsync();

        var body = await ReissuedAsync(eventId);

        foreach (var record in body.NotarialRecords)
        {
            var content = await _client.GetAsync($"v1/files/{record.StoredFileId}/content");
            content.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        }
    }

    [Fact]
    public async Task Reissue_WritesTheNextVersion_WhenTheAuthoritiesAreUnchanged()
    {
        var (eventId, _) = await CompletedAsync();
        await ReissuedAsync(eventId);

        var body = await ReissuedAsync(eventId);

        body.NotarialRecords.Select(r => r.Version).ShouldBe([3, 2, 1]);
        (await _api.GetStoredNotarialRecordsAsync(eventId)).Select(r => r.Version).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Reissue_WritesConsecutiveVersions_WhenTwoArriveAtOnce()
    {
        // El bloqueo del sorteo las pone en fila: la segunda lee la versión que escribió la primera.
        var (eventId, _) = await CompletedAsync();

        var responses = await Task.WhenAll(
            _api.ReissueAsync(eventId, CorrectedAuthorities()),
            _api.ReissueAsync(eventId, CorrectedAuthorities()));

        foreach (var response in responses)
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await _api.GetStoredNotarialRecordsAsync(eventId)).Select(r => r.Version).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Reissue_StoresTheAuthoritiesTrimmed_WhenTheyArriveWithSpaces()
    {
        var (eventId, _) = await CompletedAsync();

        var body = await ReissuedAsync(eventId, CorrectedAuthorities() with { NotaryName = "  Ana Pérez  " });

        body.NotaryName.ShouldBe("Ana Pérez");
    }

    [Fact]
    public async Task Patch_Returns409RaffleCompleted_WhenTheRaffleWasReissued()
    {
        var (eventId, _) = await CompletedAsync();
        await ReissuedAsync(eventId);

        var response = await _client.PatchAsJsonAsync(RaffleUrl(eventId), ReadyToCompleteUpdate());

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_COMPLETED");
    }

    #endregion

    #region Lo que exige

    [Fact]
    public async Task Reissue_Returns409RaffleNotCompletedAndWritesNothing_WhenInProgress()
    {
        var (eventId, _) = await _api.ReadyToCompleteAsync(_factory.Attendance);
        var rows = await _api.CountStoredFileRowsAsync();

        var response = await _api.ReissueAsync(eventId, CorrectedAuthorities());

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NOT_COMPLETED");
        (await _api.GetStoredNotarialRecordsAsync(eventId)).ShouldBeEmpty();
        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
        (await _api.GetRaffleAsync(eventId)).WitnessTwoIdentityDocument.ShouldBe("001-0000003-3");
    }

    [Fact]
    public async Task Reissue_Returns409RaffleNotCompleted_WhenTheRaffleIsADraft()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _api.ReissueAsync(eventId, CorrectedAuthorities());

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NOT_COMPLETED");
    }

    [Fact]
    public async Task Reissue_Returns404EventNotFound_WhenTheEventDoesNotExist()
    {
        var response = await _api.ReissueAsync(Guid.NewGuid(), CorrectedAuthorities());

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task Reissue_Returns404_WhenTheEventHasNoRaffle()
    {
        var eventId = await _api.CreateEventAsync();

        var response = await _api.ReissueAsync(eventId, CorrectedAuthorities());

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    public static TheoryData<string, string?> InvalidFields()
    {
        var data = new TheoryData<string, string?>();
        foreach (var field in new[]
                 {
                     nameof(ReissueRaffleRequest.NotaryName), nameof(ReissueRaffleRequest.NotaryIdentityDocument),
                     nameof(ReissueRaffleRequest.WitnessOneName), nameof(ReissueRaffleRequest.WitnessOneIdentityDocument),
                     nameof(ReissueRaffleRequest.WitnessTwoName), nameof(ReissueRaffleRequest.WitnessTwoIdentityDocument),
                 })
        {
            data.Add(field, null);
            data.Add(field, "   ");
            data.Add(field, new string('a', field.EndsWith("Name", StringComparison.Ordinal) ? 201 : 101));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task Reissue_Returns400_WhenAFieldIsMissingBlankOrTooLong(string field, string? value)
    {
        var body = CorrectedAuthorities();
        body = field switch
        {
            nameof(ReissueRaffleRequest.NotaryName) => body with { NotaryName = value },
            nameof(ReissueRaffleRequest.NotaryIdentityDocument) => body with { NotaryIdentityDocument = value },
            nameof(ReissueRaffleRequest.WitnessOneName) => body with { WitnessOneName = value },
            nameof(ReissueRaffleRequest.WitnessOneIdentityDocument) => body with { WitnessOneIdentityDocument = value },
            nameof(ReissueRaffleRequest.WitnessTwoName) => body with { WitnessTwoName = value },
            _ => body with { WitnessTwoIdentityDocument = value },
        };

        // La validación va antes que todo, así que no hace falta ni un evento.
        var response = await _api.ReissueAsync(Guid.NewGuid(), body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reissue_Returns200_WhenTheMaximumLengthsArriveExactly()
    {
        var (eventId, _) = await CompletedAsync();

        await ReissuedAsync(eventId, CorrectedAuthorities() with
        {
            NotaryName = new string('a', 200),
            NotaryIdentityDocument = new string('1', 100),
        });
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task Reissue_Returns200_WhenTheEventIsNoLongerEditable(string status)
    {
        // El notario puede ver el error días después, con el evento ya cerrado (spec 00 §5).
        var (eventId, _) = await CompletedAsync();
        await _api.SetEventStatusAsync(eventId, status);

        await ReissuedAsync(eventId);
    }

    #endregion

    #region Las caídas y los roles

    [Theory]
    [InlineData(FileStoreFault.Unavailable)]
    [InlineData(FileStoreFault.Timeout)]
    public async Task Reissue_Returns503AndLeavesTheCurrentVersion_WhenTheFileStoreIsDown(FileStoreFault fault)
    {
        var (eventId, completed) = await CompletedAsync();
        var rows = await _api.CountStoredFileRowsAsync();
        var bytes = _api.CountStoredBytes();

        HttpResponseMessage response;
        using (_factory.Failing(fault))
            response = await _api.ReissueAsync(eventId, CorrectedAuthorities());

        await ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "FILE_STORE_UNAVAILABLE");
        var read = await _api.GetRaffleAsync(eventId);
        read.NotarialRecords.ShouldHaveSingleItem().ShouldBe(completed.NotarialRecords[0]);
        read.WitnessTwoIdentityDocument.ShouldBe("001-0000003-3");
        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
        _api.CountStoredBytes().ShouldBe(bytes);
    }

    [Fact]
    public async Task Reissue_Returns403_WhenTheCallerHasRafflesWriteWithoutRafflesDraw()
    {
        var (eventId, _) = await CompletedAsync();

        var response = await _api.SendAsync(
            HttpMethod.Post, ReissueUrl(eventId), JsonContent.Create(CorrectedAuthorities()), AuthorizationPolicies.RafflesWrite);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.GetStoredNotarialRecordsAsync(eventId)).ShouldHaveSingleItem();
    }

    #endregion

    /// <summary>Un sorteo completado con su acta versión 1, y su cuerpo.</summary>
    private async Task<(Guid EventId, RaffleResponse Body)> CompletedAsync()
    {
        var (eventId, _) = await _api.ReadyToCompleteAsync(_factory.Attendance);
        var response = await _api.CompleteAsync(eventId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (eventId, (await response.Content.ReadFromJsonAsync<RaffleResponse>())!);
    }

    /// <summary>Reemite y devuelve el cuerpo, fallando el test si no es un 200.</summary>
    private async Task<RaffleResponse> ReissuedAsync(Guid eventId, ReissueRaffleRequest? body = null)
    {
        var response = await _api.ReissueAsync(eventId, body ?? CorrectedAuthorities());
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
    }
}
