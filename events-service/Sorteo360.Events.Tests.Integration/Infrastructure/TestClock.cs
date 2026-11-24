namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// El reloj de la aplicación bajo prueba. Corre con el del sistema hasta que un test fija el día:
/// el resto de la aplicación también lee este reloj, y uno detenido para todos los tests de la clase
/// podría vencer algo que no se quería vencer.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset? _pinned;

    /// <summary>Fija hoy, en UTC, al mediodía de <paramref name="today"/>.</summary>
    public void Pin(DateOnly today) =>
        _pinned = new DateTimeOffset(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    /// <summary>Fija el reloj en un instante exacto, para lo que depende de la hora y no solo del día.</summary>
    public void Pin(DateTimeOffset instant) => _pinned = instant;

    /// <summary>Mueve hacia adelante un reloj fijado.</summary>
    public void Advance(TimeSpan amount) =>
        _pinned = (_pinned ?? throw new InvalidOperationException("El reloj no está fijado.")).Add(amount);

    /// <summary>Devuelve el reloj al del sistema, para que los tests siguientes de la clase no lo hereden.</summary>
    public void Release() => _pinned = null;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _pinned ?? System.GetUtcNow();
}
