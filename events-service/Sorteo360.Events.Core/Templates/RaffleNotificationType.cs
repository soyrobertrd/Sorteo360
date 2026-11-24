namespace Sorteo360.Events.Core.Templates;

/// <summary>
/// El momento de un sorteo que manda correo. Como en <see cref="QuoteNotificationType"/>, la
/// constante es a la vez el <c>type</c> que viaja a notification-service y el <c>template_key</c> de
/// las filas sembradas.
/// </summary>
public static class RaffleNotificationType
{
    /// <summary>
    /// El sorteo se completó: cada sorteo de premio vivo le avisa a su ganador (spec 03 §5).
    /// </summary>
    public const string PrizeWon = "events.prize.won";
}
