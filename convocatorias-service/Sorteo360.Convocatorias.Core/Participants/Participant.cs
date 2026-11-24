namespace Sorteo360.Convocatorias.Core.Participants;

/// <summary>
/// Una persona de un evento: la fila de la lista de participantes (spec <c>07</c> §3). Una por
/// persona y por evento, por muchas campañas que la hayan alcanzado.
/// <para>
/// <b>Tres ejes que no se derivan entre sí</b> (§3.2): <see cref="Origin"/>,
/// <see cref="SendStatus"/> y <see cref="Registered"/>. Plegarlos en un solo estado es cómo el
/// mock terminó con una lista de espera y una puntuación que nadie pidió.
/// </para>
/// <para>
/// Abierto y pulsado no se saben (§5). La entrega sí, desde PGTI-177, pero solo cuando la lista es la
/// de una campaña: ver <see cref="DeliveryStatus"/>.
/// </para>
/// </summary>
public sealed record Participant(
    string Email,
    string FirstName,
    string LastName,
    string? Company,
    string Origin,
    string SendStatus,
    bool Registered,
    DateTimeOffset? RegisteredAt,
    string? Reference,
    string? ReferredByName,
    IReadOnlyList<Guid> CampaignIds,
    bool CrmSyncPending,
    bool DetailsDiffer)
{
    /// <summary>Los ids de mensaje de sus invitaciones entregadas al proveedor, con los que se lee la entrega.</summary>
    public IReadOnlyList<Guid> NotificationIds { get; init; } = [];

    /// <summary>
    /// Un <see cref="Campaigns.CampaignDeliveryStatus"/>, o nulo. Solo se llena en la lista de una
    /// campaña y en una fila entregada al proveedor; en la del evento entero mezclaría campañas.
    /// </summary>
    public string? DeliveryStatus { get; init; }

    /// <summary>
    /// Cuándo entró al evento (spec <c>09</c> §6). Nulo sin inscripción o sin check-in; no presentarse
    /// no tiene campo, es inscrito menos con check-in.
    /// </summary>
    public DateTimeOffset? CheckedInAt { get; init; }
}
