namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>La anulación del ganador vivo de un premio (spec 02 §3).</summary>
public record VoidPrizeDrawRequest
{
    /// <summary><c>absent</c>, <c>declined</c>, <c>ineligible</c> u <c>other</c>.</summary>
    public string? Code { get; init; }

    /// <summary>Opcional salvo con <c>other</c>. Se recorta, y en blanco se guarda como nulo.</summary>
    public string? Note { get; init; }
}
