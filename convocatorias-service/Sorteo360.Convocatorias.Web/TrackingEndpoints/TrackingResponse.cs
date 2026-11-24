using Sorteo360.Convocatorias.Core.Tracking;

namespace Sorteo360.Convocatorias.Web.TrackingEndpoints;

/// <summary>
/// El resumen de un evento, la forma de la spec <c>07</c> §2. No lleva nada de entregado, abierto,
/// pulsado ni rebotado, ni siquiera nulo (§5), ni el nombre ni las fechas del evento (§7).
/// </summary>
public sealed record TrackingResponse
{
    /// <summary>Embebidas y sin paginar: un evento tiene un puñado. Ningún borrador.</summary>
    public required IReadOnlyList<TrackingCampaignResponse> Campaigns { get; init; }

    public required TrackingPeople People { get; init; }

    public required TrackingRegistrations Registrations { get; init; }

    /// <summary>Invitados sin inscripción. Nunca cuenta entradas libres.</summary>
    public required int NotRegistered { get; init; }

    /// <summary>
    /// Inscripciones pendientes de sincronizar, fichas que no coinciden sin revisar y campañas
    /// varadas, de este evento. Nunca las bajas pendientes ni la ventana.
    /// </summary>
    public required int NeedsAttention { get; init; }

    /// <summary>Siempre presente. Un evento sin fila está abierto y sin sellos.</summary>
    public required TrackingWindowResponse RegistrationWindow { get; init; }
}

/// <summary>Una campaña del evento con sus dos cuentas.</summary>
public sealed record TrackingCampaignResponse
{
    public required Guid Id { get; init; }

    public required string TemplateKey { get; init; }

    /// <summary><c>sending</c>, <c>sent</c> o <c>cancelled</c>.</summary>
    public required string State { get; init; }

    public DateTimeOffset? SentAt { get; init; }

    public required int RecipientCount { get; init; }

    public required int ExclusionCount { get; init; }
}

/// <summary>
/// La puerta de inscripción con lo que la pantalla imprime: si está abierta, quién la cerró y cuándo.
/// Sin los sellos de la reapertura, que no pide el resumen (§2) y que las dos rutas de la ventana ya
/// devuelven.
/// </summary>
public sealed record TrackingWindowResponse(bool Open, DateTimeOffset? ClosedAt, Guid? ClosedById);
