namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// En qué está un premio. No se guarda: sale de sus sorteos y de las entregas de cada uno (spec 03
/// §1). Sin sorteo vivo está sin sortear; con uno vivo, sorteado, y entregado si ese sorteo tiene una
/// entrega viva.
/// </summary>
public static class PrizeState
{
    public const string Undrawn  = "undrawn";
    public const string Drawn    = "drawn";
    public const string Redeemed = "redeemed";

    /// <summary>El estado que dan los sorteos de un premio.</summary>
    public static string Of(IEnumerable<PrizeDraw> draws)
    {
        var liveDraw = draws.FirstOrDefault(d => d.IsLive);

        if (liveDraw is null)
            return Undrawn;

        return liveDraw.LiveHandover is null ? Drawn : Redeemed;
    }
}
