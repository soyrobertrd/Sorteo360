using System.Text.Json.Serialization;

namespace Sorteo360.Convocatorias.Web.ParticipantEndpoints;

/// <summary>
/// Una fila de la lista de participantes, la forma de la spec <c>07</c> §3. No lleva nada de
/// abierto ni pulsado, ni siquiera nulo (§5); la entrega solo aparece donde se sabe.
/// </summary>
public sealed record ParticipantResponse
{
    public required string Email { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public string? Company { get; init; }

    /// <summary><c>invited</c>, <c>referred</c> o <c>walk_in</c>.</summary>
    public required string Origin { get; init; }

    /// <summary><c>handed_off</c>, <c>pending</c>, <c>failed</c>, <c>suppressed</c> o <c>none</c>.</summary>
    public required string SendStatus { get; init; }

    public required bool Registered { get; init; }

    public DateTimeOffset? RegisteredAt { get; init; }

    public string? Reference { get; init; }

    /// <summary>Solo en una fila <c>referred</c>, y solo el nombre de quien reenvió (§3.2).</summary>
    public ParticipantReferrerResponse? ReferredBy { get; init; }

    public required IReadOnlyList<Guid> CampaignIds { get; init; }

    public required bool CrmSyncPending { get; init; }

    public required bool DetailsDiffer { get; init; }

    /// <summary>
    /// Cuándo entró al evento (spec <c>09</c> §6). Nulo sin inscripción o sin check-in, y viaja igual:
    /// acá un nulo es un dato, "no entró".
    /// </summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>
    /// <c>sent</c>, <c>delivered</c>, <c>bounced</c>, <c>spam_report</c> o <c>unsubscribed</c>
    /// (PGTI-177). Solo en la lista de una campaña y en una fila entregada al proveedor; en las demás
    /// el campo no viaja, para que nadie pinte un cero donde no se sabe nada.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeliveryStatus { get; init; }
}

/// <summary>Quién reenvió el enlace. Un objeto y no un texto, para poder crecer sin romper a nadie.</summary>
public sealed record ParticipantReferrerResponse(string Name);
