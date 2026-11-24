using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class RaffleTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 18, 0, 0, TimeSpan.Zero);

    #region CreateDraft

    [Fact]
    public void CreateDraft_ReturnsADraftWithTheAgreedDefaults_WhenCalled()
    {
        var raffle = Raffle.CreateDraft(EventId, UserId, Now);

        raffle.EventId.ShouldBe(EventId);
        raffle.State.ShouldBe("draft");
        raffle.VisualStyle.ShouldBe("roulette");
        raffle.ShowPrizeOnScreen.ShouldBeTrue();
        raffle.AnimationSeconds.ShouldBe(6);
        raffle.RequiresPresenceConfirmation.ShouldBeFalse();
        raffle.OnePrizePerPerson.ShouldBeTrue();
        raffle.OnePrizePerCompany.ShouldBeFalse();
        raffle.Authorities.ShouldBe(RaffleAuthorities.Blank);
        raffle.CreatedAt.ShouldBe(Now);
        raffle.CreatedById.ShouldBe(UserId);
    }

    [Fact]
    public void VisualStyleCatalog_ExcludesBallTombola_WhenChecked()
    {
        RaffleVisualStyle.All.ShouldBe(
            [RaffleVisualStyle.Roulette, RaffleVisualStyle.SequentialNames, RaffleVisualStyle.WinnerBoard],
            ignoreOrder: true);
        RaffleVisualStyle.All.ShouldNotContain(RaffleVisualStyle.BallTombola);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_ReturnsTheRaffleWithEveryEditableFieldReplaced_WhenDraft()
    {
        var editorId = Guid.NewGuid();
        var later = Now.AddHours(1);
        var authorities = new RaffleAuthorities("Ana Notaria", "001-1", "Luis Testigo", "002-2", null, null);
        var draft = Raffle.CreateDraft(EventId, UserId, Now);

        var result = draft.Update(UpdateDto(authorities, editorId), later);

        result.IsSuccess.ShouldBeTrue();
        var updated = result.Value;
        updated.VisualStyle.ShouldBe("sequential_names");
        updated.ShowPrizeOnScreen.ShouldBeFalse();
        updated.AnimationSeconds.ShouldBe(12);
        updated.RequiresPresenceConfirmation.ShouldBeTrue();
        // Un premio por persona no se apaga: pedir que no, deja que sí.
        updated.OnePrizePerPerson.ShouldBeTrue();
        updated.OnePrizePerCompany.ShouldBeTrue();
        updated.Authorities.ShouldBe(authorities);
        updated.PickupInstructions.ShouldBe("Oficina de MITUR, hasta el viernes.");
        updated.UpdatedAt.ShouldBe(later);
        updated.UpdatedById.ShouldBe(editorId);
        updated.CreatedAt.ShouldBe(Now);
        updated.State.ShouldBe("draft");
    }

    [Fact]
    public void Update_ReturnsThePickupInstructionsCleared_WhenTheyAreOmitted()
    {
        var withInstructions = Raffle.CreateDraft(EventId, UserId, Now) with { PickupInstructions = "Oficina" };

        var result = withInstructions.Update(UpdateDto(RaffleAuthorities.Blank, UserId) with { PickupInstructions = null }, Now);

        result.Value.PickupInstructions.ShouldBeNull();
    }

    [Fact]
    public void Update_ReturnsRaffleCompleted_WhenCompleted()
    {
        var completed = Raffle.CreateDraft(EventId, UserId, Now) with { State = RaffleState.Completed };

        var result = completed.Update(UpdateDto(RaffleAuthorities.Blank, UserId), Now.AddHours(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("RAFFLE_COMPLETED");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Update_ReturnsTheTogglesChanged_WhenInProgressAndAToggleChanges(bool flipPerson, bool flipCompany)
    {
        var inProgress = Raffle.CreateDraft(EventId, UserId, Now) with { State = RaffleState.InProgress };
        var dto = SameRulesDto(inProgress) with
        {
            OnePrizePerPerson = inProgress.OnePrizePerPerson ^ flipPerson,
            OnePrizePerCompany = inProgress.OnePrizePerCompany ^ flipCompany,
        };

        var result = inProgress.Update(dto, Now.AddHours(1));

        result.Value.OnePrizePerPerson.ShouldBeTrue();
        result.Value.OnePrizePerCompany.ShouldBe(dto.OnePrizePerCompany);
        result.Value.State.ShouldBe(RaffleState.InProgress);
    }

    [Fact]
    public void Update_ReturnsTheOtherFieldsReplaced_WhenInProgressAndTheTogglesAreUnchanged()
    {
        var inProgress = Raffle.CreateDraft(EventId, UserId, Now) with { State = RaffleState.InProgress };

        var result = inProgress.Update(SameRulesDto(inProgress), Now.AddHours(1));

        result.Value.VisualStyle.ShouldBe(RaffleVisualStyle.SequentialNames);
        result.Value.AnimationSeconds.ShouldBe(12);
    }

    [Fact]
    public void Update_ReturnsTheTogglesChanged_WhenDraft()
    {
        var draft = Raffle.CreateDraft(EventId, UserId, Now);

        var result = draft.Update(UpdateDto(RaffleAuthorities.Blank, UserId), Now.AddHours(1));

        result.Value.OnePrizePerPerson.ShouldBeTrue();
        result.Value.OnePrizePerCompany.ShouldNotBe(draft.OnePrizePerCompany);
    }

    #endregion

    #region Reopen

    [Fact]
    public void Reopen_ReturnsADraftWithTheFreezeCleared_WhenInProgress()
    {
        var started = Raffle.CreateDraft(EventId, UserId, Now).Start(Guid.NewGuid(), UserId, Now.AddHours(1)).Value;

        var result = started.Reopen();

        var reopened = result.Value;
        reopened.State.ShouldBe(RaffleState.Draft);
        reopened.StartedAt.ShouldBeNull();
        reopened.StartedById.ShouldBeNull();
        reopened.AwardsBatchId.ShouldBeNull();
        reopened.UpdatedAt.ShouldBe(started.UpdatedAt);
        reopened.UpdatedById.ShouldBe(started.UpdatedById);
    }

    [Theory]
    [InlineData(RaffleState.Draft, "RAFFLE_NOT_IN_PROGRESS")]
    [InlineData(RaffleState.Completed, "RAFFLE_COMPLETED")]
    public void Reopen_ReturnsTheStateError_WhenNotInProgress(string state, string code)
    {
        var raffle = Raffle.CreateDraft(EventId, UserId, Now) with { State = state };

        var result = raffle.Reopen();

        result.Error.Code.ShouldBe(code);
    }

    #endregion

    #region EnsureDraft

    [Fact]
    public void EnsureDraft_ReturnsSuccess_WhenDraft()
    {
        Raffle.CreateDraft(EventId, UserId, Now).EnsureDraft().IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(RaffleState.InProgress, "RAFFLE_NOT_DRAFT")]
    [InlineData(RaffleState.Completed, "RAFFLE_COMPLETED")]
    public void EnsureDraft_ReturnsTheStateError_WhenTheRaffleLeftDraft(string state, string code)
    {
        var raffle = Raffle.CreateDraft(EventId, UserId, Now) with { State = state };

        var result = raffle.EnsureDraft();

        result.Error.Code.ShouldBe(code);
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    #endregion

    #region EnsureInProgress

    [Fact]
    public void EnsureInProgress_ReturnsSuccess_WhenInProgress()
    {
        var raffle = Raffle.CreateDraft(EventId, UserId, Now) with { State = RaffleState.InProgress };

        raffle.EnsureInProgress().IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(RaffleState.Draft, "RAFFLE_NOT_IN_PROGRESS")]
    [InlineData(RaffleState.Completed, "RAFFLE_COMPLETED")]
    public void EnsureInProgress_ReturnsTheStateError_WhenNotInProgress(string state, string code)
    {
        var raffle = Raffle.CreateDraft(EventId, UserId, Now) with { State = state };

        var result = raffle.EnsureInProgress();

        result.Error.Code.ShouldBe(code);
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    #endregion

    #region Start

    [Fact]
    public void Start_ReturnsTheRaffleInProgressWithTheFreezeStamped_WhenDraft()
    {
        var starterId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var later = Now.AddHours(2);

        var result = Raffle.CreateDraft(EventId, UserId, Now).Start(batchId, starterId, later);

        var started = result.Value;
        started.State.ShouldBe(RaffleState.InProgress);
        started.StartedAt.ShouldBe(later);
        started.StartedById.ShouldBe(starterId);
        started.AwardsBatchId.ShouldBe(batchId);
        started.UpdatedAt.ShouldBe(later);
        started.UpdatedById.ShouldBe(starterId);
        started.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Start_ReturnsTheRaffleWithNoBatch_WhenNoAwardsListIsInForce()
    {
        var result = Raffle.CreateDraft(EventId, UserId, Now).Start(null, UserId, Now);

        result.Value.State.ShouldBe(RaffleState.InProgress);
        result.Value.AwardsBatchId.ShouldBeNull();
    }

    [Theory]
    [InlineData(RaffleState.InProgress, "RAFFLE_NOT_DRAFT")]
    [InlineData(RaffleState.Completed, "RAFFLE_COMPLETED")]
    public void Start_ReturnsTheStateError_WhenTheRaffleLeftDraft(string state, string code)
    {
        var raffle = Raffle.CreateDraft(EventId, UserId, Now) with { State = state };

        var result = raffle.Start(null, UserId, Now);

        result.Error.Code.ShouldBe(code);
    }

    #endregion

    #region Complete

    private static readonly RaffleAuthorities FullAuthorities =
        new("Ana Notaria", "001-1", "Luis Testigo", "002-2", "Eva Testigo", "003-3");

    [Theory]
    [InlineData(RaffleState.Draft, "RAFFLE_NOT_IN_PROGRESS")]
    [InlineData(RaffleState.Completed, "RAFFLE_COMPLETED")]
    public void Complete_ReturnsTheStateError_WhenNotInProgress(string state, string code)
    {
        var raffle = ReadyToComplete() with { State = state };

        var result = raffle.Complete(UserId, Now);

        result.Error.Code.ShouldBe(code);
    }

    public static TheoryData<int, string?> MissingAuthorityFields()
    {
        var data = new TheoryData<int, string?>();
        for (var field = 0; field < 6; field++)
        {
            data.Add(field, null);
            data.Add(field, "   ");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MissingAuthorityFields))]
    public void Complete_ReturnsRaffleAuthoritiesRequired_WhenAnAuthorityFieldIsMissing(int field, string? value)
    {
        var authorities = field switch
        {
            0 => FullAuthorities with { NotaryName = value },
            1 => FullAuthorities with { NotaryIdentityDocument = value },
            2 => FullAuthorities with { WitnessOneName = value },
            3 => FullAuthorities with { WitnessOneIdentityDocument = value },
            4 => FullAuthorities with { WitnessTwoName = value },
            _ => FullAuthorities with { WitnessTwoIdentityDocument = value },
        };
        var raffle = ReadyToComplete() with { Authorities = authorities };

        var result = raffle.Complete(UserId, Now);

        result.Error.Code.ShouldBe("RAFFLE_AUTHORITIES_REQUIRED");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public void Complete_ReturnsRaffleAuthoritiesRequired_WhenAuthoritiesAndPickupInstructionsAreBothMissing()
    {
        var raffle = ReadyToComplete() with { Authorities = RaffleAuthorities.Blank, PickupInstructions = null };

        var result = raffle.Complete(UserId, Now);

        result.Error.Code.ShouldBe("RAFFLE_AUTHORITIES_REQUIRED");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Complete_ReturnsRafflePickupInstructionsRequired_WhenTheAuthoritiesArePresentAndNoInstructions(
        string? instructions)
    {
        var raffle = ReadyToComplete() with { PickupInstructions = instructions };

        var result = raffle.Complete(UserId, Now);

        result.Error.Code.ShouldBe("RAFFLE_PICKUP_INSTRUCTIONS_REQUIRED");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public void Complete_ReturnsTheRaffleCompletedWithTheCloseStamped_WhenInProgressWithAuthoritiesAndInstructions()
    {
        var completerId = Guid.NewGuid();
        var later = Now.AddHours(3);
        var raffle = ReadyToComplete();

        var result = raffle.Complete(completerId, later);

        var completed = result.Value;
        completed.State.ShouldBe(RaffleState.Completed);
        completed.CompletedAt.ShouldBe(later);
        completed.CompletedById.ShouldBe(completerId);
        completed.UpdatedAt.ShouldBe(later);
        completed.UpdatedById.ShouldBe(completerId);
        completed.StartedAt.ShouldBe(raffle.StartedAt);
        completed.Authorities.ShouldBe(FullAuthorities);
    }

    /// <summary>En curso, con las seis autoridades y las instrucciones de retiro: lo que pide completar.</summary>
    private static Raffle ReadyToComplete() =>
        Raffle.CreateDraft(EventId, UserId, Now).Start(null, UserId, Now.AddHours(1)).Value with
        {
            Authorities = FullAuthorities,
            PickupInstructions = "Oficina de MITUR, hasta el viernes.",
        };

    #endregion

    #region Reissue

    private static readonly RaffleAuthorities CorrectedAuthorities =
        new("Ana Notaria", "001-1", "Luis Testigo", "002-2", "Eva Testigo", "003-33");

    [Theory]
    [InlineData(RaffleState.Draft)]
    [InlineData(RaffleState.InProgress)]
    public void Reissue_ReturnsRaffleNotCompleted_WhenNotCompleted(string state)
    {
        var raffle = ReadyToComplete() with { State = state };

        var result = raffle.Reissue(CorrectedAuthorities, UserId, Now);

        result.Error.Code.ShouldBe("RAFFLE_NOT_COMPLETED");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public void Reissue_ReturnsTheRaffleWithTheNewAuthoritiesStamped_WhenCompleted()
    {
        var completed = ReadyToComplete().Complete(UserId, Now.AddHours(3)).Value;
        var reissuerId = Guid.NewGuid();
        var later = Now.AddDays(2);

        var result = completed.Reissue(CorrectedAuthorities, reissuerId, later);

        var reissued = result.Value;
        reissued.State.ShouldBe(RaffleState.Completed);
        reissued.Authorities.ShouldBe(CorrectedAuthorities);
        reissued.UpdatedAt.ShouldBe(later);
        reissued.UpdatedById.ShouldBe(reissuerId);
        reissued.CompletedAt.ShouldBe(completed.CompletedAt);
        reissued.CompletedById.ShouldBe(completed.CompletedById);
    }

    #endregion

    /// <summary>Todo cambia salvo las dos reglas, que llegan con el valor guardado.</summary>
    private static UpdateRaffleDto SameRulesDto(Raffle raffle) => UpdateDto(RaffleAuthorities.Blank, UserId) with
    {
        OnePrizePerPerson = raffle.OnePrizePerPerson,
        OnePrizePerCompany = raffle.OnePrizePerCompany,
    };

    private static UpdateRaffleDto UpdateDto(RaffleAuthorities authorities, Guid updatedById) => new(
        EventId: EventId,
        VisualStyle: RaffleVisualStyle.SequentialNames,
        ShowPrizeOnScreen: false,
        AnimationSeconds: 12,
        RequiresPresenceConfirmation: true,
        OnePrizePerPerson: false,
        OnePrizePerCompany: true,
        Authorities: authorities,
        PickupInstructions: "Oficina de MITUR, hasta el viernes.",
        UpdatedById: updatedById);
}
