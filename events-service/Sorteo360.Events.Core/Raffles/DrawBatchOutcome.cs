using Sorteo360.Events.Core.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Resultado de un sorteo por lote (A33): los premios sorteados en orden y, si el bombo se agotó
/// a mitad del lote, el error <see cref="PoolExhaustedError"/> que lo explica.
/// </summary>
public sealed record DrawBatchOutcome(
    IReadOnlyList<PrizeDraw> Drawn,
    PoolExhaustedError? ExhaustedError = null);

/// <summary>
/// Resultado del servicio para un sorteo por lote con los premios completos (con sus sorteos).
/// </summary>
public sealed record DrawBatchServiceOutcome(
    IReadOnlyList<PrizeWithDraws> DrawnPrizes,
    PoolExhaustedError? ExhaustedError = null);

