using System.Text.Json.Serialization;

namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>
/// Un evento tal como lo ve un desconocido en el listado del portal.
/// <para>
/// <b>Lo que no está acá es la mitad del punto</b> (spec <c>01</c> §5): no viaja el presupuesto,
/// ni el costo total, ni <c>invitedCount</c> —que es el objetivo interno de la OPT y no un aforo—
/// ni el contacto de la OPT, ni el motivo de cancelación, ni ningún campo de auditoría. Y no
/// existe <c>mapEmbedUrl</c>: nunca existió en ninguna API, es un invento del frontend.
/// </para>
/// </summary>
public record PublicEventResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string? ExternalUrl { get; init; }
    public IReadOnlyList<PublicEventCityResponse> Cities { get; init; } = [];

    /// <summary>
    /// Si el evento todavía acepta inscripciones (spec <c>06</c> §5.3). Es el único campo que no
    /// sale de events-service: es la ventana de inscripción de este servicio, y una ventana
    /// cerrada no saca al evento de la cartelera, solo cambia esto.
    /// <para>
    /// <b>Es una pista y no la guarda</b>: le deja al portal esconder el botón, y las dos rutas de
    /// escritura siguen negándose por su cuenta. Solo lo llevan el listado y el detalle; en las
    /// respuestas de inscripción y de la entrada va nulo y no se escribe.
    /// </para>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RegistrationOpen { get; init; }
}

/// <summary>
/// El detalle. Agrega la descripción y, cuando se pudo resolver, el nombre de la OPT que
/// organiza.
/// </summary>
public sealed record PublicEventDetailResponse : PublicEventResponse
{
    public string? Description { get; init; }

    /// <summary>
    /// Quién organiza: <b>solo el nombre, nunca el id</b>. Nulo cuando el directorio del CRM
    /// estaba caído al armar la respuesta, y entonces la página omite el bloque de organizador en
    /// vez de dibujar un hueco. Sigue siendo un 200 (spec <c>01</c> §6).
    /// </summary>
    public string? Organizer { get; init; }
}

/// <summary>Una ciudad del recorrido, sin su id: a un desconocido no le sirve para nada.</summary>
public sealed record PublicEventCityResponse
{
    public string Name { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public DateOnly? Date { get; init; }
    public int? DisplayOrder { get; init; }
}
