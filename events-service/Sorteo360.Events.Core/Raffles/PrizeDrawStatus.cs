namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// En qué está un sorteo, que no es el eje del premio: un sorteo nunca está sin sortear y un premio
/// nunca está anulado (spec 03 §3.4). Es lo que responde la búsqueda por código y lo que lleva cada
/// sorteo en la historia del premio. No se guarda.
/// </summary>
public static class PrizeDrawStatus
{
    public const string Drawn    = "drawn";
    public const string Redeemed = "redeemed";
    public const string Voided   = "voided";

    /// <summary>El estado que dan la anulación del sorteo y sus entregas.</summary>
    public static string Of(PrizeDraw draw)
    {
        ArgumentNullException.ThrowIfNull(draw);

        if (!draw.IsLive)
            return Voided;

        return draw.LiveHandover is null ? Drawn : Redeemed;
    }
}
