namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Una exclusión en lote: exactamente una de las dos fuentes. Los identificadores van en el cuerpo,
/// nunca en la ruta, igual que en la decisión de a uno.
/// </summary>
public record BulkExcludeAttendeesRequest
{
    /// <summary>Excluye a los ganadores vivos del sorteo de este evento (de la misma cuenta).</summary>
    public Guid? SourceEventId { get; init; }

    /// <summary>Correos o IDs de empleado, hasta 5000. Con <c>@</c> es un correo; si no, un ID.</summary>
    public IReadOnlyList<string>? Identifiers { get; init; }

    /// <summary>Opcional. En blanco, el servicio pone de dónde salió la exclusión.</summary>
    public string? Note { get; init; }
}
