namespace Sorteo360.Platform.Tests.Unit.Http;

/// <summary>
/// Reloj controlable: la caducidad del token se prueba adelantando el reloj, nunca
/// esperando de verdad.
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>Adelanta el reloj el tiempo indicado.</summary>
    public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
}
