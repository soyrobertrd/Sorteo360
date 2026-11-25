using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class RaffleParticipantAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MapToDto_SetsExpectedExclusionReasons_ForOverrideCheckInWinnerAndDepartment()
    {
        var eventId = Guid.NewGuid();
        var raffle = Raffle.CreateDraft(eventId, Guid.NewGuid(), Now) with
        {
            OnePrizePerPerson = true,
            OnePrizePerCompany = true,
        };
        var liveDraw = new PrizeDraw(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ganador Previo", "Finanzas",
            "ganador@empresa.com", Now, Guid.NewGuid(), 10, true, true, "REF-1", null, null, null, null);
        var withRounds = new RaffleWithRounds(raffle, []) { Draws = [liveDraw] };

        var overrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["excluido@empresa.com"] = AttendeeOverrideDecision.Excluded,
        };
        var context = new RaffleParticipantWebhookEndpoints.RaffleParticipantContext(
            withRounds,
            overrides,
            new HashSet<string>(StringComparer.Ordinal) { "ganador@empresa.com" },
            new HashSet<string>(StringComparer.Ordinal) { "FINANZAS" });

        var excluded = RaffleParticipantWebhookEndpoints.MapToDto(Participant("excluido@empresa.com", true, "TI"), context);
        excluded.Qualified.ShouldBeFalse();
        excluded.ExclusionReason.ShouldBe("manually_excluded");

        var notCheckedIn = RaffleParticipantWebhookEndpoints.MapToDto(Participant("ausente@empresa.com", false, "TI"), context);
        notCheckedIn.Qualified.ShouldBeFalse();
        notCheckedIn.ExclusionReason.ShouldBe("not_checked_in");

        var alreadyWon = RaffleParticipantWebhookEndpoints.MapToDto(Participant("ganador@empresa.com", true, "TI"), context);
        alreadyWon.Qualified.ShouldBeFalse();
        alreadyWon.ExclusionReason.ShouldBe("already_won");

        var sameDepartment = RaffleParticipantWebhookEndpoints.MapToDto(Participant("colega@empresa.com", true, "Finanzas"), context);
        sameDepartment.Qualified.ShouldBeFalse();
        sameDepartment.ExclusionReason.ShouldBe("same_department");

        var eligible = RaffleParticipantWebhookEndpoints.MapToDto(Participant("ok@empresa.com", true, "Operaciones"), context);
        eligible.Qualified.ShouldBeTrue();
        eligible.ExclusionReason.ShouldBeNull();
    }

    [Fact]
    public void ComputeDrumHashAndChain_AreDeterministic_AndChangeWhenInputsChange()
    {
        var p1 = ParticipantDto("a", "uno@empresa.com");
        var p2 = ParticipantDto("b", "dos@empresa.com");

        var hashForward = RaffleParticipantWebhookEndpoints.ComputeDrumHash([p1, p2]);
        var hashReverse = RaffleParticipantWebhookEndpoints.ComputeDrumHash([p2, p1]);
        hashForward.ShouldBe(hashReverse);
        hashForward.Length.ShouldBe(64);

        var draw = new PrizeDraw(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Uno", "IT",
            "uno@empresa.com", Now, Guid.NewGuid(), 2, true, false, "ABC-123", null, null, null, null);

        var chainEmpty = RaffleParticipantWebhookEndpoints.ComputeDrawChainHash(hashForward, []);
        var chainOne = RaffleParticipantWebhookEndpoints.ComputeDrawChainHash(hashForward, [draw]);
        chainEmpty.ShouldBe(hashForward);
        chainOne.ShouldNotBe(hashForward);
        chainOne.Length.ShouldBe(64);
    }

    private static RaffleParticipant Participant(string email, bool checkedIn, string department) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Nombre Apellido",
        email,
        company: "Empresa",
        department: department,
        employeeCode: null,
        phone: null,
        qrCode: "qr-" + email,
        checkedIn: checkedIn,
        checkedInAt: checkedIn ? Now : null,
        source: "excel",
        createdAt: Now,
        updatedAt: Now);

    private static RaffleParticipantDto ParticipantDto(string id, string email) => new()
    {
        Id = id,
        FullName = "Persona",
        Email = email,
        IdentityKey = email,
        QrCode = "qr-" + id,
        CheckedIn = true,
        Qualified = true,
    };
}

