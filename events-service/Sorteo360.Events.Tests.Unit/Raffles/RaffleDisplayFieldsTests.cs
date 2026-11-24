using Shouldly;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class RaffleDisplayFieldsTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 18, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_UsesFullNameTicketNumberAndLocation()
    {
        RaffleDisplayFields.Default.Name.ShouldBe(RaffleDisplayFields.NameFullName);
        RaffleDisplayFields.Default.Identifier.ShouldBe(RaffleDisplayFields.IdentifierTicketNumber);
        RaffleDisplayFields.Default.Detail.ShouldBe(RaffleDisplayFields.DetailLocation);
    }

    [Fact]
    public void NormalizeOrDefault_FallsBackToDefault_WhenValuesAreInvalid()
    {
        var normalized = RaffleDisplayFields.NormalizeOrDefault("invalid", null, "unknown");

        normalized.ShouldBe(RaffleDisplayFields.Default);
    }

    [Fact]
    public void ChangeDisplayFields_Succeeds_WhenRaffleIsInDraft()
    {
        var raffle = Raffle.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Now);
        var updatedFields = new RaffleDisplayFields(
            RaffleDisplayFields.NameFirstAndLast,
            RaffleDisplayFields.IdentifierEmployeeCode,
            RaffleDisplayFields.DetailLocation);

        var result = raffle.ChangeDisplayFields(updatedFields, Guid.NewGuid(), Now.AddMinutes(5));

        result.IsSuccess.ShouldBeTrue();
        result.Value.DisplayFields.ShouldBe(updatedFields);
    }

    [Theory]
    [InlineData(RaffleState.InProgress)]
    [InlineData(RaffleState.Completed)]
    public void ChangeDisplayFields_ReturnsConflict_WhenRaffleIsNotInDraft(string state)
    {
        var raffle = Raffle.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Now) with { State = state };
        var updatedFields = new RaffleDisplayFields(
            RaffleDisplayFields.NameFirstName,
            RaffleDisplayFields.IdentifierNone,
            RaffleDisplayFields.DetailCompany);

        var result = raffle.ChangeDisplayFields(updatedFields, Guid.NewGuid(), Now.AddMinutes(5));

        result.IsFailure.ShouldBeTrue();
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }
}

