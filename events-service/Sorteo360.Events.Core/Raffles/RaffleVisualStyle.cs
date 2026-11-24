namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Cómo se muestra el sorteo en pantalla. Se guarda para que la laptop del operador y el proyector
/// coincidan, y ninguna regla del dominio lo lee (spec 00 §7). No existe <c>tombola</c>: es un
/// valor heredado que ningún cliente escribe, y el frontend ya lo reescribe a <c>ball_tombola</c>.
/// </summary>
public static class RaffleVisualStyle
{
    public const string Roulette        = "roulette";
    public const string SequentialNames = "sequential_names";
    public const string WinnerBoard     = "winner_board";

    /// <summary>Valor heredado de tómbola retirado en Sorteo360 (se migra a <see cref="Roulette"/> al leer y escribir).</summary>
    public const string BallTombola     = "ball_tombola";

    /// <summary>
    /// Distingue mayúsculas: el valor viaja tal cual a la base, cuyo CHECK compara exacto.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Roulette, SequentialNames, WinnerBoard
    };
}
