namespace Sorteo360.Events.Tests.Unit.Shared;

/// <summary>
/// Un reloj que solo avanza cuando el test lo dice.
/// <para>
/// Existe porque las vigencias se prueban haciéndolas vencer, y esperar de verdad convertiría cada
/// uno de esos tests en minutos. Vive acá y no junto a la primera suite que lo necesitó para que
/// la segunda lo use en vez de copiarlo.
/// </para>
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Mueve el reloj hacia adelante.</summary>
    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
}
