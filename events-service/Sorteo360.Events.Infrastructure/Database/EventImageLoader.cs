using Microsoft.EntityFrameworkCore;
using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// La lectura de las imágenes de un evento con la ficha de su archivo, en un solo sitio.
/// <para>
/// La comparten dos repositorios: el de imágenes, que la sirve por evento, y el de eventos, que
/// la necesita porque las imágenes viajan con el evento en el detalle y la portada en el
/// listado. Es un helper y no una llamada de un repositorio al otro para que ninguno de los dos
/// dependa del otro; la consulta es la misma y el <c>DbContext</c> también.
/// </para>
/// </summary>
internal static class EventImageLoader
{
    /// <summary>
    /// Las imágenes vivas de esos eventos, agrupadas por evento, la portada primero y el resto
    /// del más antiguo al más reciente. Un evento sin imágenes simplemente no aparece en el
    /// diccionario.
    /// <para>
    /// <paramref name="kind"/> nulo trae las de los dos tipos, que es lo que pide la ficha del
    /// evento; el listado pide solo la portada.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<EventImageWithFile>>> LoadAsync(
        EventsDbContext context,
        IReadOnlyList<Guid> eventIds,
        EventImageKind? kind,
        CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<EventImageWithFile>>();

        var query = context.EventImages
            .AsNoTracking()
            .Where(i => eventIds.Contains(i.EventId));

        if (kind is not null)
            query = query.Where(i => i.Kind == kind);

        var rows = await query
            .Join(
                context.StoredFiles.AsNoTracking(),
                i => i.StoredFileId,
                f => f.Id,
                (image, file) => new { Image = image, File = file })
            // La portada encabeza la lista: quien la muestra la toma de la primera posición sin
            // recorrer nada, y el resto queda en el orden en que se subió.
            .OrderBy(x => x.Image.Kind == EventImageKind.Cover ? 0 : 1)
            .ThenBy(x => x.Image.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.Image.EventId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<EventImageWithFile>)g
                    .Select(x => new EventImageWithFile(
                        MapToDomain(x.Image),
                        StoredFileMapper.ToDomain(x.File)))
                    .ToList());
    }

    /// <summary>
    /// La portada de cada uno de esos eventos, que es lo único que el listado enseña. Un evento
    /// sin portada no aparece en el diccionario.
    /// <para>
    /// Existe para que quien llama no tenga que saber que la lista viene con la portada primero
    /// ni que un grupo nunca está vacío: las dos cosas son de acá.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, EventImageWithFile>> LoadCoversAsync(
        EventsDbContext context,
        IReadOnlyList<Guid> eventIds,
        CancellationToken cancellationToken)
    {
        var byEvent = await LoadAsync(context, eventIds, EventImageKind.Cover, cancellationToken);

        // Una portada viva por evento la garantiza el índice único parcial, así que cada grupo
        // trae exactamente una.
        return byEvent.ToDictionary(pair => pair.Key, pair => pair.Value[0]);
    }

    /// <summary>Convierte una fila de enlace en la entidad de dominio.</summary>
    private static EventImage MapToDomain(EventImageData d)
    {
        return new EventImage(
            id:           d.Id,
            eventId:      d.EventId,
            storedFileId: d.StoredFileId,
            kind:         d.Kind,
            createdAt:    d.CreatedAt,
            updatedAt:    d.UpdatedAt,
            createdById:  d.CreatedById,
            updatedById:  d.UpdatedById,
            deletedAt:    d.DeletedAt);
    }
}
