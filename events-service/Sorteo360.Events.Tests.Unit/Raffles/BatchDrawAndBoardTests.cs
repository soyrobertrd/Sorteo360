using Shouldly;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.RaffleEndpoints;
using Sorteo360.Events.Web.Validators;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class BatchDrawAndBoardTests
{
    [Fact]
    public void PrizeDrawSummary_From_propagates_winner_board_fields()
    {
        var now = DateTimeOffset.UtcNow;
        var draw = new PrizeDraw(
            Id: Guid.NewGuid(),
            PrizeId: Guid.NewGuid(),
            WinnerAttendeeId: Guid.NewGuid(),
            WinnerName: "Ana Pérez",
            WinnerCompany: "Banreservas",
            WinnerIdentityKey: "ana@banreservas.com",
            DrawnAt: now,
            DrawnById: Guid.NewGuid(),
            EligiblePoolSize: 250,
            OnePrizePerPerson: true,
            OnePrizePerCompany: false,
            RedemptionCode: "ABCD-1234",
            VoidCode: null,
            VoidNote: null,
            VoidedAt: null,
            VoidedById: null)
        {
            WinnerTicketNumber = "482-917",
            WinnerEmployeeCode = "EMP-0042",
            WinnerDepartment = "Tecnología",
        };

        var summary = PrizeDrawSummary.From(draw);

        summary.WinnerTicketNumber.ShouldBe("482-917");
        summary.WinnerEmployeeCode.ShouldBe("EMP-0042");
        summary.WinnerDepartment.ShouldBe("Tecnología");
    }

    [Fact]
    public void EligibleResponse_From_applies_limit_and_keeps_full_count()
    {
        var now = DateTimeOffset.UtcNow;
        var attendees = Enumerable.Range(1, 150)
            .Select(i => new RaffleAttendee(
                Id: Guid.NewGuid(),
                IdentityKey: $"user{i}@example.com",
                ContactId: null,
                FirstName: "User",
                LastName: i.ToString(),
                Company: "Dept",
                CheckedInAt: now,
                Qualified: true,
                ExclusionReason: null,
                ManualDecision: null,
                ManualDecidedById: null,
                ManualDecidedAt: null,
                ManualNote: null,
                OverruledReason: null))
            .ToList();

        var pool = EligiblePool.For(attendees, raffleDraws: [], onePrizePerCompany: false);

        var limited = EligibleResponse.From(pool, limit: 36);
        limited.Count.ShouldBe(150);
        limited.Participants.Count.ShouldBe(36);
        limited.Participants.Select(p => p.AttendeeId).Distinct().Count().ShouldBe(36);

        var clamped = EligibleResponse.From(pool, limit: 500);
        clamped.Count.ShouldBe(150);
        clamped.Participants.Count.ShouldBe(EligibleResponse.MaxLimit);
    }

    [Fact]
    public void DrawBatchPrizesRequestValidator_enforces_1_to_10_unique_ids()
    {
        var validator = new DrawBatchPrizesRequestValidator();

        validator.Validate(new DrawBatchPrizesRequest { PrizeIds = [] }).IsValid.ShouldBeFalse();

        var dup = Guid.NewGuid();
        validator.Validate(new DrawBatchPrizesRequest { PrizeIds = [dup, dup] }).IsValid.ShouldBeFalse();

        var eleven = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList();
        validator.Validate(new DrawBatchPrizesRequest { PrizeIds = eleven }).IsValid.ShouldBeFalse();

        var ten = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        validator.Validate(new DrawBatchPrizesRequest { PrizeIds = ten }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void PrizeResponse_From_exposes_image_url_when_image_file_id_is_present()
    {
        var now = DateTimeOffset.UtcNow;
        var imageId = Guid.NewGuid();
        var withImage = new Prize(
            id: Guid.NewGuid(),
            roundId: Guid.NewGuid(),
            name: "Bono RD$10,000",
            description: null,
            value: "RD$10,000",
            createdAt: now,
            updatedAt: now,
            createdById: Guid.NewGuid(),
            updatedById: Guid.NewGuid(),
            imageStoredFileId: imageId);

        var withoutImage = withImage with { ImageStoredFileId = null };

        PrizeResponse.From(withImage).ImageUrl.ShouldBe($"/v1/files/{imageId}/content");
        PrizeResponse.From(withoutImage).ImageUrl.ShouldBeNull();
    }
}
