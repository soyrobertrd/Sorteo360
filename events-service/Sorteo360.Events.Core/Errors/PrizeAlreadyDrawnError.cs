using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Errors;

/// <summary>
/// El premio ya tiene un ganador vivo. Es también la idempotencia del sorteo (spec 02 §7.1): el
/// error lleva ese sorteo, así que un reintento tras una respuesta perdida recupera al ganador sin
/// otra llamada, y nunca sortea dos veces.
/// </summary>
public sealed record PrizeAlreadyDrawnError : BaseError
{
    public const string ErrorCode = "PRIZE_ALREADY_DRAWN";

    /// <summary>El sorteo vivo, en la forma de la respuesta: sin correo.</summary>
    public PrizeDrawSummary Draw { get; init; }

    public PrizeAlreadyDrawnError(PrizeDraw liveDraw)
        : base("Este premio ya tiene un ganador.", ErrorCode, ErrorKind.Conflict)
    {
        ArgumentNullException.ThrowIfNull(liveDraw);

        Draw = PrizeDrawSummary.From(liveDraw);
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["draw"] = Draw,
        };
}
