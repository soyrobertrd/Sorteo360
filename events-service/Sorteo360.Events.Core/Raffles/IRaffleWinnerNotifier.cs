using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Le avisa por correo al ganador de un sorteo de premio que ganó, cuando el sorteo se completa
/// (spec 03 §5). Uno por sorteo vivo; un sorteo anulado no llega acá.
/// </summary>
public interface IRaffleWinnerNotifier
{
    /// <summary>
    /// Manda el correo al ganador de <paramref name="draw"/>, a la dirección con que se lo inscribió,
    /// en el idioma del evento si hay copia y en español si no.
    /// <para>
    /// <b>Nunca lanza, cancelación incluida.</b> Corre después de confirmar el sorteo, así que toda
    /// avería termina en el desenlace devuelto y en el log, y completar sigue siendo un éxito
    /// (ADR-0020).
    /// </para>
    /// </summary>
    Task<QuoteNotificationOutcome> NotifyWonAsync(
        Event @event,
        Raffle raffle,
        Prize prize,
        PrizeDraw draw,
        CancellationToken cancellationToken);
}
