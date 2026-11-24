namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Cuándo se vuelve a intentar un envío, y cuándo se deja de intentar.
/// <para>
/// Se abandona a la hora, no a la semana. Un aviso sobre una cotización que se decidió ayer tiene
/// valor negativo, y correo rancio es peor que correo ausente.
/// </para>
/// </summary>
public static class SendRetryPolicy
{
    /// <summary>Desde que la fila entró en cola hasta que se deja de intentar.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromHours(1);

    /// <summary>Espera del primer reintento. Las siguientes la duplican.</summary>
    public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Techo de la espera. Sin él la exponencial se pasaría de la hora en un solo salto y el
    /// último intento nunca llegaría a ocurrir.
    /// </summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(10);

    /// <summary>Motivo que queda escrito en la fila que se abandonó.</summary>
    public const string GaveUpReason =
        "Se dejó de reintentar: pasó una hora desde que el envío entró en cola.";

    /// <summary>
    /// Cuánto esperar antes del intento número <paramref name="attemptCount"/> + 1. Exponencial
    /// desde <see cref="BaseDelay"/> y con tope en <see cref="MaxDelay"/>.
    /// </summary>
    public static TimeSpan DelayAfter(int attemptCount)
    {
        var exponent = Math.Max(0, attemptCount - 1);

        // Se acota el exponente antes de elevar: sin esto una fila con muchos intentos desborda
        // el double antes de que el tope llegue a aplicarse.
        var capped = Math.Min(exponent, 16);
        var seconds = BaseDelay.TotalSeconds * Math.Pow(2, capped);

        return seconds >= MaxDelay.TotalSeconds ? MaxDelay : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Si ya pasó la hora desde que la fila entró en cola.</summary>
    public static bool HasGivenUp(DateTimeOffset queuedAt, DateTimeOffset now) =>
        now - queuedAt >= GiveUpAfter;

    /// <summary>
    /// Cuándo toca el siguiente intento, o null si ese intento caería fuera de la hora: entonces
    /// no hay siguiente intento y la fila es terminal. Programarlo igual dejaría filas que el
    /// worker recoge solo para volver a descartarlas.
    /// </summary>
    public static DateTimeOffset? NextAttemptAt(DateTimeOffset queuedAt, DateTimeOffset now, int attemptCount)
    {
        var next = now + DelayAfter(attemptCount);

        return next - queuedAt >= GiveUpAfter ? null : next;
    }
}
