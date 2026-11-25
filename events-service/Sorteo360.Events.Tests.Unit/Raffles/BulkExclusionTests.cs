using Sorteo360.Events.Core.Raffles;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// La clasificación de la exclusión en lote: a quién se excluye y qué se informa, contra el padrón y
/// la puerta del destino, sin base.
/// </summary>
public sealed class BulkExclusionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReadIdentifiers_TrimsDropsBlanksAndRepeats_AndSplitsEmailsFromIds()
    {
        var (candidates, invalid) = BulkExclusion.ReadIdentifiers(
            ["  Ana@Banco.com ", "ana@banco.com", "", "   ", null, "E-10234", "e-10234", "no@es", "@"]);

        candidates.Select(c => (c.Email, c.Id)).ShouldBe([("ana@banco.com", null), (null, "e-10234")]);
        invalid.ShouldBe(["no@es", "@"]);
    }

    [Fact]
    public void Classify_ExcludesThePresent_AndReportsMissingAndInvalid()
    {
        var (candidates, invalid) = BulkExclusion.ReadIdentifiers(
            ["ANA@banco.com", "E-55", "x@y.com", "E-999", "luis@banco.com"]);
        RosterPerson[] roster =
        [
            new("ana@banco.com", "ana@banco.com", "E-1"),
            new("id:e-55", null, "E-55"),
            new("luis@banco.com", "luis@banco.com", null),
        ];

        var (outcome, keys) = BulkExclusion.Classify(
            candidates, invalid, [], roster,
            new HashSet<string> { "ana@banco.com", "id:e-55" }, []);

        keys.ShouldBe(["ana@banco.com", "id:e-55"]);
        outcome.Excluded.ShouldBe(2);
        outcome.AlreadyExcluded.ShouldBe(0);
        // luis está en el padrón pero no en la puerta; x@y.com no está; E-999 no es de nadie.
        outcome.NotInRoster.ShouldBe(["x@y.com", "luis@banco.com"]);
        outcome.Invalid.ShouldBe(["E-999"]);
        outcome.Unidentifiable.ShouldBeEmpty();
    }

    [Fact]
    public void Classify_CountsTheAlreadyExcluded_AndTurnsAnInclusionIntoAnExclusion()
    {
        var (candidates, invalid) = BulkExclusion.ReadIdentifiers(["ana@banco.com", "beto@banco.com"]);
        RosterPerson[] roster = [new("ana@banco.com", "ana@banco.com", null), new("beto@banco.com", "beto@banco.com", null)];
        AttendeeOverride[] overrides =
        [
            new("ana@banco.com", AttendeeOverrideDecision.Excluded, null, Guid.NewGuid(), Now),
            new("beto@banco.com", AttendeeOverrideDecision.Included, null, Guid.NewGuid(), Now),
        ];

        var (outcome, keys) = BulkExclusion.Classify(
            candidates, invalid, [], roster, new HashSet<string> { "ana@banco.com", "beto@banco.com" }, overrides);

        keys.ShouldBe(["beto@banco.com"]);
        outcome.Excluded.ShouldBe(1);
        outcome.AlreadyExcluded.ShouldBe(1);
    }

    [Fact]
    public void Classify_CountsAPersonOnce_WhenHerEmailAndHerIdBothCome()
    {
        var (candidates, invalid) = BulkExclusion.ReadIdentifiers(["ana@banco.com", "E-1"]);
        RosterPerson[] roster = [new("ana@banco.com", "ana@banco.com", "E-1")];

        var (outcome, keys) = BulkExclusion.Classify(
            candidates, invalid, [], roster, new HashSet<string> { "ana@banco.com" }, []);

        keys.ShouldBe(["ana@banco.com"]);
        outcome.Excluded.ShouldBe(1);
        outcome.NotInRoster.ShouldBeEmpty();
        outcome.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void Classify_FindsAWinnerByIdWhenTheDestinationHasNoEmailForHer()
    {
        // Un ganador de otro evento: correo e ID; en el destino entró solo con el ID.
        BulkExclusion.Candidate[] winners = [new("pedro@banco.com", "pedro@banco.com", "e-55"), new("x@y.com", "x@y.com", null)];
        RosterPerson[] roster = [new("id:e-55", null, "E-55")];

        var (outcome, keys) = BulkExclusion.Classify(
            winners, [], ["María Pérez"], roster, new HashSet<string> { "id:e-55" }, []);

        keys.ShouldBe(["id:e-55"]);
        // Un ganador que no está en el destino se informa como fuera del padrón, nunca como inválido.
        outcome.NotInRoster.ShouldBe(["x@y.com"]);
        outcome.Invalid.ShouldBeEmpty();
        outcome.Unidentifiable.ShouldBe(["María Pérez"]);
    }
}
