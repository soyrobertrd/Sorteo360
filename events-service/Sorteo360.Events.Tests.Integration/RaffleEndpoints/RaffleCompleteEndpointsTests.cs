using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>POST …/raffle/complete</c>: el cierre del sorteo y el acta que lo atestigua (spec 03 §4.4). Un
/// sorteo completado siempre tiene acta, y un intento fallido no deja nada escrito.
/// </summary>
public sealed class RaffleCompleteEndpointsTests : IClassFixture<RaffleCompletionApiDbFactory>
{
    private readonly RaffleCompletionApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleCompleteEndpointsTests(RaffleCompletionApiDbFactory factory)
    {
        _factory = factory;
        _factory.Realm.ServeDefaults();
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    #region El cierre

    [Fact]
    public async Task Complete_Returns200WithTheRaffleCompletedAndItsRecord_WhenAPrizeIsStillUndrawn()
    {
        var (eventId, _) = await _api.ReadyToCompleteAsync(_factory.Attendance);

        var response = await _api.CompleteAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
        body.State.ShouldBe("completed");
        body.CompletedById.ShouldBe(TestAuthHandler.DefaultUserId);
        body.CompletedAt.ShouldNotBeNull();
        var record = body.NotarialRecords.ShouldHaveSingleItem();
        record.Version.ShouldBe(1);
        record.GeneratedById.ShouldBe(TestAuthHandler.DefaultUserId);
        record.GeneratedAt.ShouldBe(body.CompletedAt.Value);

        var read = await _api.GetRaffleAsync(eventId);
        read.State.ShouldBe("completed");
        read.NotarialRecords.ShouldHaveSingleItem().StoredFileId.ShouldBe(record.StoredFileId);
        (await _api.GetStoredNotarialRecordsAsync(eventId)).ShouldHaveSingleItem().Version.ShouldBe(1);
    }

    [Fact]
    public async Task Complete_StoresAPdfThatTheFlatFileRouteServes_WhenItSucceeds()
    {
        var (eventId, _) = await _api.ReadyToCompleteAsync(_factory.Attendance);

        var body = await CompletedAsync(eventId);

        var content = await _client.GetAsync($"v1/files/{body.NotarialRecords[0].StoredFileId}/content");
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        var bytes = await content.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task Complete_Returns409RaffleNotInProgress_WhenTheRaffleIsADraft()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());

        var response = await _api.CompleteAsync(eventId);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NOT_IN_PROGRESS");
        (await _api.GetStoredNotarialRecordsAsync(eventId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Complete_Returns404EventNotFound_WhenTheEventDoesNotExist()
    {
        var response = await _api.CompleteAsync(Guid.NewGuid());

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task Complete_Returns404_WhenTheEventHasNoRaffle()
    {
        var eventId = await _api.CreateEventAsync();

        var response = await _api.CompleteAsync(eventId);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    #endregion

    #region Lo que exige

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Complete_Returns409AuthoritiesRequiredAndWritesNothing_WhenAWitnessIsMissing(string? witness)
    {
        var (eventId, _) = await _api.ReadyToCompleteAsync(_factory.Attendance);
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate() with { WitnessTwoIdentityDocument = witness });

        await ShouldWriteNothingAsync(eventId, () => _api.CompleteAsync(eventId), "RAFFLE_AUTHORITIES_REQUIRED");
    }

    [Fact]
    public async Task Complete_Returns409PickupInstructionsRequiredAndWritesNothing_WhenTheyAreMissing()
    {
        // Con un ganador, para que "no manda ningún correo" diga algo.
        var (eventId, prizeId) = await _api.ReadyToCompleteAsync(_factory.Attendance);
        (await _api.DrawAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate() with { PickupInstructions = null });
        _factory.Notifications.ResetRequestLog();

        await ShouldWriteNothingAsync(eventId, () => _api.CompleteAsync(eventId), "RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED");
        _factory.Notifications.SendKeys.ShouldBeEmpty();
    }

    #endregion

    #region Después de completar

    public static TheoryData<string> RefusedOnACompletedRaffle =>
    [
        "complete", "patch", "create-round", "update-round", "delete-round", "create-prize", "update-prize",
        "delete-prize", "draw", "void", "reopen",
    ];

    [Theory]
    [MemberData(nameof(RefusedOnACompletedRaffle))]
    public async Task Write_Returns409RaffleCompleted_WhenTheRaffleIsCompleted(string write)
    {
        var (eventId, drawn, undrawn) = await CompletedWithADrawAsync();

        var response = write switch
        {
            "complete" => await _api.CompleteAsync(eventId),
            "patch" => await _client.PatchAsJsonAsync(RaffleUrl(eventId), ReadyToCompleteUpdate()),
            "create-round" => await _client.PostAsJsonAsync(RoundsUrl(eventId), new CreateRaffleRoundRequest { Name = "Extra" }),
            "create-prize" => await _client.PostAsJsonAsync(
                PrizesUrl(eventId),
                new CreatePrizesRequest { RoundId = (await _api.GetRaffleAsync(eventId)).Rounds[0].Id, Name = "Extra", Quantity = 1 }),
            "update-round" => await _client.PatchAsJsonAsync(
                $"{RoundsUrl(eventId)}/{(await _api.GetRaffleAsync(eventId)).Rounds[0].Id}",
                new UpdateRaffleRoundRequest { Name = "Otra" }),
            "delete-round" => await _client.DeleteAsync($"{RoundsUrl(eventId)}/{(await _api.GetRaffleAsync(eventId)).Rounds[0].Id}"),
            "update-prize" => await _client.PatchAsJsonAsync(
                PrizeUrl(eventId, undrawn),
                new UpdatePrizeRequest { RoundId = (await _api.GetRaffleAsync(eventId)).Rounds[0].Id, Name = "Otro" }),
            "delete-prize" => await _client.DeleteAsync(PrizeUrl(eventId, undrawn)),
            "draw" => await _api.DrawAsync(eventId, undrawn),
            "void" => await _api.VoidAsync(eventId, drawn, PrizeDrawVoidCode.Absent),
            "reopen" => await _api.ReopenAsync(eventId),
            _ => throw new ArgumentOutOfRangeException(nameof(write)),
        };

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_COMPLETED");
        (await _api.GetStoredNotarialRecordsAsync(eventId)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task StartAndOverride_Return409RaffleCompleted_WhenTheRaffleIsCompleted()
    {
        // Lo que exige draft dice RAFFLE_NOT_DRAFT en curso, pero sobre un completado gana el error
        // más específico, como en toda escritura salvo la entrega (spec 00, definición de terminado).
        var (eventId, _, _) = await CompletedWithADrawAsync();

        await ShouldBeProblemAsync(await _api.StartAsync(eventId), HttpStatusCode.Conflict, "RAFFLE_COMPLETED");
        await ShouldBeProblemAsync(
            await _api.SetOverrideAsync(eventId, "ana@agencia.com", "excluded", "Pedido del cliente"),
            HttpStatusCode.Conflict, "RAFFLE_COMPLETED");
        await ShouldBeProblemAsync(
            await _api.RemoveOverrideAsync(eventId, "ana@agencia.com"), HttpStatusCode.Conflict, "RAFFLE_COMPLETED");
    }

    [Fact]
    public async Task Start_Returns409RaffleNotDraft_WhenStartedTwice()
    {
        var (eventId, _) = await _api.ReadyToCompleteAsync(_factory.Attendance);

        var response = await _api.StartAsync(eventId);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "RAFFLE_NOT_DRAFT");
    }

    [Fact]
    public async Task HandOver_Returns200_WhenTheRaffleIsCompleted()
    {
        var (eventId, drawn, _) = await CompletedWithADrawAsync();

        var response = await _api.HandOverAsync(eventId, drawn);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region El estado del evento

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task DrawVoidCompleteAndHandOver_Return200_WhenTheEventIsNoLongerEditable(string status)
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 3, prizes: 2);
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());
        await _api.SetEventStatusAsync(eventId, status);

        (await _api.DrawAsync(eventId, prizes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.VoidAsync(eventId, prizes[0], PrizeDrawVoidCode.Absent)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.DrawAsync(eventId, prizes[1])).StatusCode.ShouldBe(HttpStatusCode.OK);
        var complete = await _api.CompleteAsync(eventId);
        complete.StatusCode.ShouldBe(HttpStatusCode.OK, await complete.Content.ReadAsStringAsync());
        var handover = await _api.HandOverAsync(eventId, prizes[1]);
        handover.StatusCode.ShouldBe(HttpStatusCode.OK, await handover.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task CreatePatchAndStart_Return409EventNotEditable_WhenTheEventIsNoLongerEditable(string status)
    {
        var withoutRaffle = await _api.CreateEventAsync();
        var withDraft = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(withDraft, FakeAttendanceSource.Invitees(2));
        await _api.SetEventStatusAsync(withoutRaffle, status);
        await _api.SetEventStatusAsync(withDraft, status);

        await ShouldBeProblemAsync(
            await _client.PostAsync(RaffleUrl(withoutRaffle), content: null), HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
        await ShouldBeProblemAsync(
            await _client.PatchAsJsonAsync(RaffleUrl(withDraft), ReadyToCompleteUpdate()), HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
        await ShouldBeProblemAsync(await _api.StartAsync(withDraft), HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
    }

    #endregion

    #region Las caídas

    [Theory]
    [InlineData(FileStoreFault.Unavailable)]
    [InlineData(FileStoreFault.Timeout)]
    public async Task Complete_Returns503AndWritesNothingThenSucceeds_WhenTheFileStoreIsDownAndComesBack(FileStoreFault fault)
    {
        var (eventId, prizeId) = await _api.ReadyToCompleteAsync(_factory.Attendance);
        (await _api.DrawAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var rows = await _api.CountStoredFileRowsAsync();
        var bytes = _api.CountStoredBytes();
        _factory.Notifications.ResetRequestLog();

        HttpResponseMessage response;
        using (_factory.Failing(fault))
            response = await _api.CompleteAsync(eventId);

        await ShouldBeProblemAsync(response, HttpStatusCode.ServiceUnavailable, "FILE_STORE_UNAVAILABLE");
        _factory.Notifications.SendKeys.ShouldBeEmpty();
        (await _api.GetStoredRaffleAsync(eventId)).State.ShouldBe("in_progress");
        (await _api.GetStoredNotarialRecordsAsync(eventId)).ShouldBeEmpty();
        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
        _api.CountStoredBytes().ShouldBe(bytes);

        var retry = await _api.CompleteAsync(eventId);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Complete_Returns200_WhenTheRealmDoesNotKnowTheDecider()
    {
        // Quien tomó la decisión manual no es nadie en el realm simulado, que contesta 404 por él.
        var eventId = await WithAManualDecisionReadyToCompleteAsync();

        var response = await _api.CompleteAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Complete_Returns200_WhenTheRealmFailsForTheDecider()
    {
        var eventId = await WithAManualDecisionReadyToCompleteAsync();
        _factory.Realm.FailUser(TestAuthHandler.DefaultUserId.ToString());

        var response = await _api.CompleteAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Roles

    [Fact]
    public async Task DrawWrites_Return403_WhenTheCallerHasRafflesWriteWithoutRafflesDraw()
    {
        var (eventId, drawn, undrawn) = await StartedWithADrawAsync();
        var write = AuthorizationPolicies.RafflesWrite;

        (await _api.SendAsync(HttpMethod.Post, CompleteUrl(eventId), null, write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, StartUrl(eventId), null, write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, DrawUrl(eventId, undrawn), null, write)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, VoidUrl(eventId, drawn), JsonContent.Create(new VoidPrizeDrawRequest { Code = PrizeDrawVoidCode.Absent }), write))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, HandoverUrl(eventId, drawn), HandoverForm(), write))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Patch, RaffleUrl(eventId), JsonContent.Create(ReadyToCompleteUpdate()), write))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DrawWrites_Return200AndPatchReturns403_WhenTheCallerHasRafflesDrawWithoutRafflesWrite()
    {
        // El armado lo hace quien tiene raffles:write; la noche entera, quien tiene raffles:draw.
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(3));
        var prizes = await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), quantity: 2);
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());
        var draw = AuthorizationPolicies.RafflesDraw;

        await ShouldBeOkAsync(await _api.SendAsync(HttpMethod.Post, StartUrl(eventId), null, draw));
        await ShouldBeOkAsync(await _api.SendAsync(HttpMethod.Post, DrawUrl(eventId, prizes[0].Id), null, draw));
        await ShouldBeOkAsync(await _api.SendAsync(
            HttpMethod.Post, VoidUrl(eventId, prizes[0].Id),
            JsonContent.Create(new VoidPrizeDrawRequest { Code = PrizeDrawVoidCode.Absent }), draw));
        await ShouldBeOkAsync(await _api.SendAsync(HttpMethod.Post, DrawUrl(eventId, prizes[1].Id), null, draw));
        await ShouldBeOkAsync(await _api.SendAsync(HttpMethod.Post, HandoverUrl(eventId, prizes[1].Id), HandoverForm(), draw));
        (await _api.SendAsync(HttpMethod.Patch, RaffleUrl(eventId), JsonContent.Create(ReadyToCompleteUpdate()), draw))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ShouldBeOkAsync(await _api.SendAsync(HttpMethod.Post, CompleteUrl(eventId), null, draw));
    }

    #endregion

    #region Las instrucciones de retiro

    [Fact]
    public async Task Patch_ReturnsTheInstructionsTrimmed_WhenTheyAreSent()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate() with { PickupInstructions = "  Stand de MITUR  " });

        (await _api.GetRaffleAsync(eventId)).PickupInstructions.ShouldBe("Stand de MITUR");
    }

    [Fact]
    public async Task Patch_ClearsTheInstructions_WhenTheyAreOmitted()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());

        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate() with { PickupInstructions = null });

        (await _api.GetRaffleAsync(eventId)).PickupInstructions.ShouldBeNull();
    }

    [Fact]
    public async Task Patch_Returns400_WhenTheInstructionsAreOver1000Characters()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await _client.PatchAsJsonAsync(
            RaffleUrl(eventId), ReadyToCompleteUpdate() with { PickupInstructions = new string('a', 1001) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Lo que no existe

    [Theory]
    [InlineData("raffle", "acta_stored_file_id")]
    [InlineData("prize_draw", "redeemed_at")]
    [InlineData("prize_draw", "redeemed_by_id")]
    public async Task Schema_HasNoColumn_WhenTheSpecPutsTheFactElsewhere(string table, string column)
    {
        // El acta vive en notarial_record, versionada, y la entrega en prize_handover (spec 03 §1).
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var count = await context.Database
            .SqlQuery<int>(
                $"""
                SELECT count(*)::int AS "Value" FROM information_schema.columns
                WHERE table_schema = 'events' AND table_name = {table}
                  AND (column_name = {column} OR column_name LIKE 'redeemed%')
                """)
            .SingleAsync();

        count.ShouldBe(0);
    }

    [Fact]
    public void Routes_HaveNoRedeemByCodeAndNoRaffleSpecificDownload_WhenListed()
    {
        // Un código solo encuentra un sorteo, y los bytes del acta salen de la ruta plana de archivos.
        var routes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => (Pattern: e.RoutePattern.RawText ?? string.Empty,
                Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []))
            .ToList();

        routes.Where(r => r.Pattern.Contains("by-code") && r.Methods.Any(m => m != "GET")).ShouldBeEmpty();
        routes.Where(r => r.Pattern.Contains("raffle")
                && (r.Pattern.Contains("content") || r.Pattern.Contains("download")
                    || r.Pattern.Contains("notarial") || r.Pattern.Contains("acta")))
            .ShouldBeEmpty();
    }

    #endregion

    private static async Task ShouldBeOkAsync(HttpResponseMessage response) =>
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

    /// <summary>
    /// Un sorteo listo para completar con una exclusión manual antes de arrancar, tomada por el
    /// usuario de los tests: el acta tiene que nombrar a quien la tomó.
    /// </summary>
    private async Task<Guid> WithAManualDecisionReadyToCompleteAsync()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(3));
        var exclude = await _api.SetOverrideAsync(eventId, "persona0@agencia.com", AttendeeOverrideDecision.Excluded, "Pedido del cliente");
        exclude.StatusCode.ShouldBe(HttpStatusCode.NoContent, await exclude.Content.ReadAsStringAsync());
        await ShouldBeOkAsync(await _api.StartAsync(eventId));
        await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId));
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());
        return eventId;
    }

    /// <summary>Completa y devuelve el cuerpo, fallando el test si no es un 200.</summary>
    private async Task<RaffleResponse> CompletedAsync(Guid eventId)
    {
        var response = await _api.CompleteAsync(eventId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RaffleResponse>())!;
    }

    /// <summary>Un sorteo listo para completar, con un premio sorteado y otro sin sortear.</summary>
    private async Task<(Guid EventId, Guid Drawn, Guid Undrawn)> StartedWithADrawAsync()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 3, prizes: 2);
        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());
        var draw = await _api.DrawAsync(eventId, prizes[0]);
        draw.StatusCode.ShouldBe(HttpStatusCode.OK, await draw.Content.ReadAsStringAsync());
        return (eventId, prizes[0], prizes[1]);
    }

    /// <summary>Lo mismo, ya completado.</summary>
    private async Task<(Guid EventId, Guid Drawn, Guid Undrawn)> CompletedWithADrawAsync()
    {
        var raffle = await StartedWithADrawAsync();
        await CompletedAsync(raffle.EventId);
        return raffle;
    }

    /// <summary>
    /// Un rechazo de <c>complete</c> deja todo como estaba: el sorteo en curso, sin acta, sin fila de
    /// archivo y sin bytes.
    /// </summary>
    private async Task ShouldWriteNothingAsync(Guid eventId, Func<Task<HttpResponseMessage>> complete, string code)
    {
        var rows = await _api.CountStoredFileRowsAsync();
        var bytes = _api.CountStoredBytes();

        await ShouldBeProblemAsync(await complete(), HttpStatusCode.Conflict, code);

        (await _api.GetStoredRaffleAsync(eventId)).State.ShouldBe("in_progress");
        (await _api.GetStoredNotarialRecordsAsync(eventId)).ShouldBeEmpty();
        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
        _api.CountStoredBytes().ShouldBe(bytes);
    }
}
