using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.RaffleEndpoints;
using Npgsql;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>POST …/prizes/{id}/handover</c> y <c>…/handover/reverse</c> sobre un sorteo arrancado de verdad
/// (spec 03 §3). Cada rechazo comprueba que no quedó ni la fila ni la firma, y las carreras corren
/// contra la base.
/// </summary>
public sealed class PrizeHandoverEndpointsTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private const int OneMegabyte = 1024 * 1024;

    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public PrizeHandoverEndpointsTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    #region La entrega

    [Fact]
    public async Task HandOver_Returns200WithThePrizeRedeemed_WhenThePrizeIsDrawn()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        var draw = (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem();

        var response = await _api.HandOverAsync(eventId, prizeId, RaffleTestApi.HandoverForm(
            receiverName: "  Ana Gómez  ", receiverIdentityDocument: " 001-7654321-0 ", receiverIsWinner: "FALSE"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var prize = (await response.Content.ReadFromJsonAsync<PrizeResponse>())!;
        prize.State.ShouldBe(PrizeState.Redeemed);
        var drawn = prize.Draws.ShouldHaveSingleItem();
        drawn.RedemptionCode.ShouldBe(draw.RedemptionCode);
        drawn.Status.ShouldBe(PrizeDrawStatus.Redeemed);
        var handover = drawn.Handovers.ShouldHaveSingleItem();
        handover.ReceiverName.ShouldBe("Ana Gómez");
        handover.ReceiverIdentityDocument.ShouldBe("001-7654321-0");
        handover.ReceiverIsWinner.ShouldBeFalse();
        handover.ReversedAt.ShouldBeNull();

        var stored = (await _api.GetStoredHandoversAsync(eventId)).ShouldHaveSingleItem();
        stored.PrizeDrawId.ShouldBe(draw.Id);
        stored.SignatureStoredFileId.ShouldBe(handover.SignatureStoredFileId);
        stored.RedeemedById.ShouldNotBe(Guid.Empty);

        var content = await _client.GetAsync($"v1/files/{stored.SignatureStoredFileId}/content");
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await content.Content.ReadAsByteArrayAsync()).ShouldBe(SampleFiles.Png());

        var fromRead = (await _api.GetRaffleAsync(eventId)).Rounds.SelectMany(r => r.Prizes).Single(p => p.Id == prizeId);
        fromRead.State.ShouldBe(PrizeState.Redeemed);
        fromRead.Draws.ShouldHaveSingleItem().Handovers.ShouldHaveSingleItem().ShouldBe(handover);
    }

    [Fact]
    public async Task HandOver_Returns200_WhenTheRaffleIsCompleted()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        await _api.SetRaffleStateAsync(eventId, RaffleState.Completed);

        var response = await _api.HandOverAsync(eventId, prizeId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HandOver_Returns200_WhenTheEventIsClosed()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        await _api.SetEventStatusAsync(eventId, EventStatus.Closed);

        var response = await _api.HandOverAsync(eventId, prizeId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region La firma

    public static TheoryData<string, string, string, string> RefusedSignatures => new()
    {
        { "jpeg", "firma.jpg", "image/jpeg", "FILE_TYPE_NOT_ALLOWED" },
        { "pdf", "firma.pdf", "application/pdf", "FILE_TYPE_NOT_ALLOWED" },
        { "large-png", "firma.png", "image/png", "FILE_TOO_LARGE" },
        { "jpeg", "firma.png", "image/png", "FILE_TYPE_MISMATCH" },
    };

    [Theory]
    [MemberData(nameof(RefusedSignatures))]
    public async Task HandOver_Returns400WithTheSeamsCodeAndLeavesNothing_WhenTheSignatureIsRefused(
        string kind, string fileName, string contentType, string code)
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        var bytes = kind switch
        {
            "jpeg" => SampleFiles.Jpeg(),
            "pdf" => SampleFiles.Pdf(),
            _ => Padded(SampleFiles.Png(), OneMegabyte + 1),
        };
        var (rows, files) = (await _api.CountStoredFileRowsAsync(), _api.CountStoredBytes());

        var response = await _api.HandOverAsync(eventId, prizeId, RaffleTestApi.HandoverForm(
            signature: bytes, signatureFileName: fileName, signatureContentType: contentType));

        await RaffleTestApi.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, code);
        await ShouldHaveWrittenNothingAsync(eventId, rows, files);
    }

    [Fact]
    public async Task HandOver_Returns200_WhenThePngIsExactlyOneMegabyte()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);

        var response = await _api.HandOverAsync(eventId, prizeId, RaffleTestApi.HandoverForm(
            signature: Padded(SampleFiles.Png(), OneMegabyte)));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Validación

    public static TheoryData<string, MultipartFormDataContent> InvalidForms => new()
    {
        { "receiverIdentityDocument", RaffleTestApi.HandoverForm(receiverIdentityDocument: null) },
        { "receiverIdentityDocument", RaffleTestApi.HandoverForm(receiverIsWinner: "true", receiverIdentityDocument: "   ") },
        { "receiverIsWinner", RaffleTestApi.HandoverForm(receiverIsWinner: null) },
        { "receiverIsWinner", RaffleTestApi.HandoverForm(receiverIsWinner: "yes") },
        { "receiverIsWinner", RaffleTestApi.HandoverForm(receiverIsWinner: "1") },
        { "receiverName", RaffleTestApi.HandoverForm(receiverName: null) },
        { "receiverName", RaffleTestApi.HandoverForm(receiverName: new string('x', 201)) },
        { "receiverIdentityDocument", RaffleTestApi.HandoverForm(receiverIdentityDocument: new string('x', 101)) },
        { "signature", RaffleTestApi.HandoverForm(withSignature: false) },
        { "signature", RaffleTestApi.HandoverForm(signature: []) },
    };

    [Theory]
    [MemberData(nameof(InvalidForms))]
    public async Task HandOver_Returns400NamingTheFieldAndLeavesNothing_WhenTheFormIsInvalid(
        string field, MultipartFormDataContent form)
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        var (rows, files) = (await _api.CountStoredFileRowsAsync(), _api.CountStoredBytes());

        var response = await _api.HandOverAsync(eventId, prizeId, form);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.EnumerateObject().Select(e => e.Name)
            .ShouldContain(name => string.Equals(name, field, StringComparison.OrdinalIgnoreCase));
        await ShouldHaveWrittenNothingAsync(eventId, rows, files);
    }

    [Fact]
    public async Task HandOver_Returns200_WhenTheFieldsAreAtTheirLimitsAfterTrimming()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);

        var response = await _api.HandOverAsync(eventId, prizeId, RaffleTestApi.HandoverForm(
            receiverName: $" {new string('x', 200)} ", receiverIdentityDocument: $" {new string('y', 100)} "));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Rechazos por estado

    [Fact]
    public async Task HandOver_Returns409PrizeNotDrawnAndLeavesNothing_WhenThePrizeIsUndrawn()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        var (rows, files) = (await _api.CountStoredFileRowsAsync(), _api.CountStoredBytes());

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.HandOverAsync(eventId, prizes[0]), HttpStatusCode.Conflict, "PRIZE_NOT_DRAWN");

        await ShouldHaveWrittenNothingAsync(eventId, rows, files);
    }

    [Fact]
    public async Task HandOver_Returns409PrizeNotDrawn_WhenTheOnlyDrawIsVoided()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.VoidAsync(eventId, prizeId, PrizeDrawVoidCode.Absent)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.HandOverAsync(eventId, prizeId), HttpStatusCode.Conflict, "PRIZE_NOT_DRAWN");
    }

    [Fact]
    public async Task HandOver_Returns409PrizeAlreadyRedeemedAndLeavesNoSecondSignature_WhenThePrizeIsRedeemed()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var (rows, files) = (await _api.CountStoredFileRowsAsync(), _api.CountStoredBytes());

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.HandOverAsync(eventId, prizeId), HttpStatusCode.Conflict, "PRIZE_ALREADY_REDEEMED");

        (await _api.CountStoredFileRowsAsync()).ShouldBe(rows);
        _api.CountStoredBytes().ShouldBe(files);
        (await _api.GetStoredHandoversAsync(eventId)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task HandOver_Returns404PrizeNotFound_WhenThePrizeIsAnotherRaffles()
    {
        var (eventId, _) = await _api.DrawnPrizeAsync(_factory.Attendance);
        var (_, otherPrizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.HandOverAsync(eventId, otherPrizeId), HttpStatusCode.NotFound, "PRIZE_NOT_FOUND");
    }

    [Fact]
    public async Task Void_Returns409PrizeAlreadyRedeemedAndChangesNothing_WhenThePrizeIsRedeemed()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.VoidAsync(eventId, prizeId, PrizeDrawVoidCode.Absent), HttpStatusCode.Conflict, "PRIZE_ALREADY_REDEEMED");

        (await _api.GetStoredDrawsAsync(eventId)).ShouldHaveSingleItem().VoidedAt.ShouldBeNull();
        (await _api.GetStoredHandoversAsync(eventId)).ShouldHaveSingleItem().ReversedAt.ShouldBeNull();
    }

    #endregion

    #region La reversión

    [Fact]
    public async Task Reverse_Returns200KeepingTheRowAndTheSignature_WhenThePrizeIsRedeemed()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _api.ReverseHandoverAsync(eventId, prizeId, "  Se marcó el premio equivocado.  ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var prize = (await response.Content.ReadFromJsonAsync<PrizeResponse>())!;
        prize.State.ShouldBe(PrizeState.Drawn);
        var draw = prize.Draws.ShouldHaveSingleItem();
        draw.Status.ShouldBe(PrizeDrawStatus.Drawn);
        draw.Handovers.ShouldHaveSingleItem().ReversalReason.ShouldBe("Se marcó el premio equivocado.");

        var stored = (await _api.GetStoredHandoversAsync(eventId)).ShouldHaveSingleItem();
        stored.ReversedAt.ShouldNotBeNull();
        stored.ReversedById.ShouldNotBeNull();
        stored.ReversalReason.ShouldBe("Se marcó el premio equivocado.");
        (await _client.GetAsync($"v1/files/{stored.SignatureStoredFileId}/content")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HandOver_Returns200_WhenTheEarlierHandoverWasReversed()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.ReverseHandoverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _api.HandOverAsync(eventId, prizeId, RaffleTestApi.HandoverForm(receiverName: "Ana Gómez"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var handovers = (await response.Content.ReadFromJsonAsync<PrizeResponse>())!.Draws.Single().Handovers;
        handovers.Count.ShouldBe(2);
        handovers[0].ReceiverName.ShouldBe("Ana Gómez");
        handovers[0].ReversedAt.ShouldBeNull();
        handovers[1].ReversedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Reverse_Returns200_WhenTheRaffleIsCompletedAndTheEventClosed()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await _api.SetRaffleStateAsync(eventId, RaffleState.Completed);
        await _api.SetEventStatusAsync(eventId, EventStatus.Closed);

        var response = await _api.ReverseHandoverAsync(eventId, prizeId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reverse_Returns409PrizeNotRedeemed_WhenThePrizeIsDrawn()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.ReverseHandoverAsync(eventId, prizeId), HttpStatusCode.Conflict, "PRIZE_NOT_REDEEMED");
    }

    [Fact]
    public async Task Reverse_Returns409PrizeNotRedeemed_WhenThePrizeIsUndrawn()
    {
        var (eventId, prizes) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);

        await RaffleTestApi.ShouldBeProblemAsync(
            await _api.ReverseHandoverAsync(eventId, prizes[0]), HttpStatusCode.Conflict, "PRIZE_NOT_REDEEMED");
    }

    public static TheoryData<string?> InvalidReasons => new() { null, "   ", new string('x', 501) };

    [Theory]
    [MemberData(nameof(InvalidReasons))]
    public async Task Reverse_Returns400AndChangesNothing_WhenTheReasonIsInvalid(string? reason)
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _api.ReverseHandoverAsync(eventId, prizeId, reason);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await _api.GetStoredHandoversAsync(eventId)).ShouldHaveSingleItem().ReversedAt.ShouldBeNull();
    }

    #endregion

    #region La base

    [Fact]
    public async Task Insert_ReturnsUniqueViolation_WhenADrawWouldHaveTwoLiveHandovers()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        (await _api.HandOverAsync(eventId, prizeId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var live = (await _api.GetStoredHandoversAsync(eventId)).ShouldHaveSingleItem();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var exception = await Should.ThrowAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO events.prize_handover
                (id, prize_draw_id, receiver_name, receiver_identity_document, receiver_is_winner,
                 signature_stored_file_id, redeemed_at, redeemed_by_id)
            VALUES ({Guid.NewGuid()}, {live.PrizeDrawId}, 'Otra', '002', false,
                    {live.SignatureStoredFileId}, now(), {Guid.NewGuid()})
            """));

        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        exception.ConstraintName.ShouldBe("ux_prize_handover_prize_draw_id_live");
    }

    #endregion

    #region Permisos

    [Fact]
    public async Task HandOverAndReverse_Returns403AndWritesNothing_WhenTheCallerOnlyHoldsRafflesWrite()
    {
        var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
        var (rows, files) = (await _api.CountStoredFileRowsAsync(), _api.CountStoredBytes());

        var handover = await _api.SendAsync(
            HttpMethod.Post, RaffleTestApi.HandoverUrl(eventId, prizeId), RaffleTestApi.HandoverForm(),
            AuthorizationPolicies.RafflesWrite);
        var reverse = await _api.SendAsync(
            HttpMethod.Post, RaffleTestApi.ReverseHandoverUrl(eventId, prizeId),
            JsonContent.Create(new ReversePrizeHandoverRequest { Reason = "Motivo" }),
            AuthorizationPolicies.RafflesWrite);

        handover.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        reverse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ShouldHaveWrittenNothingAsync(eventId, rows, files);
    }

    #endregion

    #region Concurrencia

    /// <summary>
    /// Una anulación y una entrega sobre el mismo premio se ordenan en el bloqueo: o la anulación entra
    /// antes y la entrega no encuentra ganador, o la entrega entra antes y la anulación lo encuentra
    /// entregado. Nunca una firma sobre un sorteo anulado.
    /// </summary>
    [Fact]
    public async Task VoidAndHandOver_ReturnOneSuccessAndOneConflict_WhenTheyRaceOnTheSamePrize()
    {
        for (var run = 0; run < 10; run++)
        {
            var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
            var (rows, files) = (await _api.CountStoredFileRowsAsync(), _api.CountStoredBytes());

            var responses = await Task.WhenAll(
                _api.VoidAsync(eventId, prizeId, PrizeDrawVoidCode.Absent), _api.HandOverAsync(eventId, prizeId));
            var (voided, handedOver) = (responses[0], responses[1]);

            if (voided.StatusCode == HttpStatusCode.OK)
            {
                await RaffleTestApi.ShouldBeProblemAsync(handedOver, HttpStatusCode.Conflict, "PRIZE_NOT_DRAWN");
                await ShouldHaveWrittenNothingAsync(eventId, rows, files);
            }
            else
            {
                handedOver.StatusCode.ShouldBe(HttpStatusCode.OK, $"vuelta {run}: {await handedOver.Content.ReadAsStringAsync()}");
                await RaffleTestApi.ShouldBeProblemAsync(voided, HttpStatusCode.Conflict, "PRIZE_ALREADY_REDEEMED");
            }

            var draws = await _api.GetStoredDrawsAsync(eventId);
            var handovers = await _api.GetStoredHandoversAsync(eventId);
            handovers.ShouldNotContain(h => draws.Single(d => d.Id == h.PrizeDrawId).VoidedAt != null);
        }
    }

    /// <summary>Dos mostradores entregando el mismo premio: uno entra, el otro lo encuentra entregado.</summary>
    [Fact]
    public async Task HandOver_ReturnsOneSuccessAndLeavesOneSignature_WhenTwoRaceOnTheSamePrize()
    {
        for (var run = 0; run < 10; run++)
        {
            var (eventId, prizeId) = await _api.DrawnPrizeAsync(_factory.Attendance);
            var (rows, files) = (await _api.CountStoredFileRowsAsync(), _api.CountStoredBytes());

            var responses = await Task.WhenAll(_api.HandOverAsync(eventId, prizeId), _api.HandOverAsync(eventId, prizeId));

            responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1, $"vuelta {run}");
            await RaffleTestApi.ShouldBeProblemAsync(
                responses.Single(r => r.StatusCode != HttpStatusCode.OK), HttpStatusCode.Conflict, "PRIZE_ALREADY_REDEEMED");
            (await _api.GetStoredHandoversAsync(eventId)).ShouldHaveSingleItem();
            (await _api.CountStoredFileRowsAsync()).ShouldBe(rows + 1);
            _api.CountStoredBytes().ShouldBe(files + 1);
        }
    }

    #endregion

    private async Task ShouldHaveWrittenNothingAsync(Guid eventId, int storedFileRows, int storedFiles)
    {
        (await _api.GetStoredHandoversAsync(eventId)).ShouldBeEmpty();
        (await _api.CountStoredFileRowsAsync()).ShouldBe(storedFileRows);
        _api.CountStoredBytes().ShouldBe(storedFiles);
    }

    private static byte[] Padded(byte[] header, int length)
    {
        var bytes = new byte[length];
        header.CopyTo(bytes, 0);
        return bytes;
    }
}
