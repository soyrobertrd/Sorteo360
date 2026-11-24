using Sorteo360.Notifications.Core.Sending;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Sending;

/// <summary>
/// Cuándo se reintenta y cuándo se deja de reintentar. La hora es un límite duro: un aviso sobre
/// algo que se decidió ayer tiene valor negativo.
/// </summary>
public sealed class SendRetryPolicyTests
{
    private static readonly DateTimeOffset QueuedAt = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GiveUpAfter_ReturnsOneHour_WhenRead() =>
        SendRetryPolicy.GiveUpAfter.ShouldBe(TimeSpan.FromHours(1));

    [Fact]
    public void HasGivenUp_ReturnsFalse_WhenTheHourHasNotPassed() =>
        SendRetryPolicy.HasGivenUp(QueuedAt, QueuedAt.AddMinutes(59)).ShouldBeFalse();

    [Fact]
    public void HasGivenUp_ReturnsTrue_WhenTheHourIsReached() =>
        SendRetryPolicy.HasGivenUp(QueuedAt, QueuedAt.AddHours(1)).ShouldBeTrue();

    [Fact]
    public void DelayAfter_ReturnsDoubleTheWait_WhenAnotherAttemptFails()
    {
        SendRetryPolicy.DelayAfter(1).ShouldBe(TimeSpan.FromSeconds(30));
        SendRetryPolicy.DelayAfter(2).ShouldBe(TimeSpan.FromSeconds(60));
        SendRetryPolicy.DelayAfter(3).ShouldBe(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void DelayAfter_ReturnsTheCeiling_WhenTheWaitWouldPassIt() =>
        // Sin techo la exponencial se pasaría de la hora en un salto y el último intento nunca
        // llegaría a ocurrir.
        SendRetryPolicy.DelayAfter(20).ShouldBe(TimeSpan.FromMinutes(10));

    [Fact]
    public void NextAttemptAt_ReturnsTheBackoffInstant_WhenTheAttemptFallsInsideTheHour()
    {
        var next = SendRetryPolicy.NextAttemptAt(QueuedAt, QueuedAt.AddMinutes(1), attemptCount: 1);

        next.ShouldBe(QueuedAt.AddMinutes(1).AddSeconds(30));
    }

    [Fact]
    public void NextAttemptAt_ReturnsNull_WhenTheAttemptWouldFallPastTheHour()
    {
        // Programarlo igual dejaría filas que el worker recoge solo para volver a descartarlas.
        var next = SendRetryPolicy.NextAttemptAt(QueuedAt, QueuedAt.AddMinutes(58), attemptCount: 8);

        next.ShouldBeNull();
    }
}
