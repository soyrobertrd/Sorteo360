using Shouldly;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class TicketNumberGeneratorTests
{
    [Theory]
    [InlineData(0, "000-000")]
    [InlineData(42, "000-042")]
    [InlineData(482917, "482-917")]
    [InlineData(999999, "999-999")]
    public void Format_ReturnsZeroPaddedHyphenatedSixDigits_WhenInRange(int value, string expected)
    {
        TicketNumberGenerator.Format(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public void Format_ThrowsArgumentOutOfRange_WhenOutOfRange(int value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => TicketNumberGenerator.Format(value));
    }

    [Fact]
    public void NextUnique_RetriesOnCollisionAndReturnsFirstUnusedTicket()
    {
        var used = new HashSet<string>(StringComparer.Ordinal) { "123-456" };
        var sequence = new Queue<int>([123456, 123456, 482917]);

        var ticket = TicketNumberGenerator.NextUnique(used, () => sequence.Dequeue());

        ticket.ShouldBe("482-917");
    }

    [Fact]
    public void NextUnique_Throws_WhenAllAttemptsCollide()
    {
        var used = new HashSet<string>(StringComparer.Ordinal) { "555-555" };

        Should.Throw<InvalidOperationException>(() => TicketNumberGenerator.NextUnique(used, () => 555555));
    }

    [Fact]
    public void NextUnique_GeneratesValidFormatWithCryptoRandom_WhenNoGeneratorProvided()
    {
        var ticket = TicketNumberGenerator.NextUnique(new HashSet<string>());

        ticket.Length.ShouldBe(7);
        ticket[3].ShouldBe('-');
        ticket.Where((_, idx) => idx != 3).All(char.IsAsciiDigit).ShouldBeTrue();
    }
}

