using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// El bloqueo de la fila del evento que serializa las escrituras del monto final contra sí mismas
/// y contra el cierre del evento.
/// </summary>
/// <remarks>
/// Es el mismo modismo que el sorteo (ADR-0028): se bloquea la fila padre <c>FOR UPDATE</c> al
/// abrir la transacción, y recién entonces se vuelve a leer y se decide. El token xmin del evento
/// no alcanza, porque las reglas se comprueban sobre lecturas sin seguimiento hechas antes de
/// sellarlo. Lo comparten el repositorio de cotizaciones y el de eventos, que es donde está el
/// cierre.
/// </remarks>
internal static class EventRowLock
{
    /// <summary>
    /// Bloquea la fila del evento hasta el final de la transacción en curso y devuelve su estado
    /// tal como quedó bajo el bloqueo, o <see cref="Maybe{T}.None"/> si el evento no existe o fue
    /// eliminado.
    /// </summary>
    /// <remarks>
    /// Devuelve el estado y no un sí o un no porque cada escritura exige uno distinto: las del
    /// monto final, planning o in_progress; el cierre, solo in_progress. Tiene que llamarse dentro
    /// de una transacción abierta, o el bloqueo se suelta al terminar la sentencia.
    /// </remarks>
    public static async Task<Maybe<string>> LockAsync(EventsDbContext context, Guid eventId, CancellationToken cancellationToken)
    {
        var status = await context.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM events.event WHERE id = {eventId} AND deleted_at IS NULL FOR UPDATE")
            .ToListAsync(cancellationToken);

        return status.Count == 0 ? Maybe<string>.None : Maybe.From(status[0]);
    }
}
