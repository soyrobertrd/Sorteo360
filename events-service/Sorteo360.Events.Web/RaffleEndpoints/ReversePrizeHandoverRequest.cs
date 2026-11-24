namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>La reversión de la entrega viva de un premio (spec 03 §3.3).</summary>
public record ReversePrizeHandoverRequest
{
    /// <summary>Por qué, de hasta 500 caracteres. Obligatorio; se recorta.</summary>
    public string? Reason { get; init; }
}
