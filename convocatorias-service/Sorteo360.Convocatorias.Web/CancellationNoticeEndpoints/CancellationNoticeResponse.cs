namespace Sorteo360.Convocatorias.Web.CancellationNoticeEndpoints;

/// <summary>Lo que hizo una pasada del aviso de cancelación (spec <c>06</c> §4.7).</summary>
public sealed record CancellationNoticeResponse
{
    public Guid EventId { get; init; }

    /// <summary>A cuántas personas les salió el aviso en esta pasada. Cero en una segunda llamada.</summary>
    public int Notified { get; init; }

    /// <summary>
    /// A cuántas no. Siguen sin avisar, así que la próxima llamada las vuelve a intentar.
    /// </summary>
    public int Failed { get; init; }
}
