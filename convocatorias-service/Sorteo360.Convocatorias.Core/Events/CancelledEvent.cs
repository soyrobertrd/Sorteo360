namespace Sorteo360.Convocatorias.Core.Events;

/// <summary>
/// Un evento cancelado, visto desde la reconciliación (spec <c>07</c> §6.5).
/// </summary>
/// <param name="Id">El evento.</param>
/// <param name="CancelledAt">
/// El <c>updatedAt</c> que da events-service. Es la cancelación porque cancelar es terminal y
/// después ninguna edición pasa; nulo si events-service no lo mandó.
/// </param>
public sealed record CancelledEvent(Guid Id, DateTimeOffset? CancelledAt);
