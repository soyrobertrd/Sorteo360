using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Database;

/// <summary>
/// Contrato de persistencia para el agregado Evento.
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Persiste un nuevo evento junto con su ciudad inicial en una sola transacción.
    /// Devuelve el identificador del evento creado, o un error de infraestructura.
    /// </summary>
    Task<Result<Guid, BaseError>> CreateAsync(Event @event, RoadshowCity city, CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene un evento junto con sus ciudades por su identificador,
    /// excluyendo los registros con soft-delete.
    /// Devuelve <see cref="Maybe{T}.None"/> cuando el evento no existe, sin lanzar excepción.
    /// Devuelve un error de infraestructura si la consulta falla.
    /// </summary>
    Task<Result<Maybe<EventWithCities>, BaseError>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene solo el estado del evento (<c>planning</c>, <c>in_progress</c>,
    /// <c>closed</c> o <c>cancelled</c>), excluyendo los registros con soft-delete.
    /// Devuelve <see cref="Maybe{T}.None"/> cuando el evento no existe. Es la lectura
    /// que usan los subflujos que solo necesitan saber si el evento admite escrituras,
    /// sin cargar el agregado con sus ciudades.
    /// </summary>
    Task<Result<Maybe<string>, BaseError>> GetStatusAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Inicia un evento: fija su estado en <c>in_progress</c>. Devuelve el evento actualizado
    /// con sus ciudades, o <see cref="Maybe{T}.None"/> si el evento no existe. La validación
    /// del estado de origen ocurre en la capa de servicio; aquí solo se persiste.
    /// </summary>
    Task<Result<Maybe<EventWithCities>, BaseError>> StartAsync(Guid eventId, Guid startedById, CancellationToken cancellationToken);

    /// <summary>
    /// Cancela un evento: fija su estado en <c>cancelled</c>, persiste la razón de cancelación
    /// y marca como eliminadas (soft-delete) las cotizaciones no terminales del evento, todo en
    /// una sola transacción. Devuelve el evento actualizado con sus ciudades, o
    /// <see cref="Maybe{T}.None"/> si el evento no existe. La validación del estado de origen
    /// ocurre en la capa de servicio; aquí solo se persiste.
    /// </summary>
    Task<Result<Maybe<EventWithCities>, BaseError>> CancelAsync(Guid eventId, string reason, Guid cancelledById, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda el evento que produjo <see cref="Event.Update"/> y lo devuelve con sus ciudades, o
    /// <see cref="Maybe{T}.None"/> si el evento ya no existe.
    /// </summary>
    /// <remarks>
    /// Copia sobre la fila solo los campos que una edición es dueña de cambiar, nunca el estado, el
    /// costo total ni la razón de cancelación. Las reglas de la edición viven en el evento; aquí solo
    /// se persiste, salvo un caso: si la fila se cerró o se canceló después de que quien llama la
    /// leyó, responde <c>EVENT_NOT_EDITABLE</c> y no escribe nada.
    /// </remarks>
    Task<Result<Maybe<EventWithCities>, BaseError>> UpdateAsync(Event @event, CancellationToken cancellationToken);

    /// <summary>
    /// Cierra un evento fijando su estado en <c>closed</c> y materializando su costo total.
    /// </summary>
    /// <remarks>
    /// Devuelve el evento actualizado con sus ciudades, o <see cref="Maybe{T}.None"/> si el
    /// evento no existe. Bloquea la fila del evento y exige que siga en in_progress
    /// (<c>EVENT_NOT_CLOSABLE</c>) antes de pasarlo a cerrado.
    /// </remarks>
    Task<Result<Maybe<EventWithCities>, BaseError>> CloseAsync(
        Guid eventId,
        Guid closedById,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lista eventos de forma paginada aplicando los filtros opcionales de <paramref nombre="filter"/>
    /// (tipo, status o "activos", y ventana de fechas sobre <c>StartDate</c>), excluyendo los
    /// registros con soft-delete. Devuelve la página de eventos junto con el total de
    /// coincidencias, o un error de infraestructura si la consulta falla. Los elementos del
    /// listado no incluyen el agregado de ciudades ni la galería: solo el evento y su portada,
    /// por eso se devuelve <see cref="EventWithCover"/>.
    /// </summary>
    Task<Result<PagedResult<EventWithCover>, BaseError>> ListAsync(ListEventsDto filter, CancellationToken cancellationToken);

    /// <summary>
    /// Lista los eventos no cancelados cuyo rango <c>[StartDate, EndDate]</c> se cruza con
    /// <c>[<paramref name="from"/>, <paramref name="to"/>]</c>, ambos extremos incluidos, con sus
    /// ciudades y ordenados por <c>StartDate</c> y luego por nombre. Excluye los registros con
    /// soft-delete. Sin paginar: quien pregunta acota la ventana.
    /// </summary>
    Task<Result<IReadOnlyList<DoorEvent>, BaseError>> ListOverlappingAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Actualiza los campos mutables de una ciudad (nombre, pais, fecha, display order).
    /// Devuelve <c>false</c> si la ciudad no existe o fue eliminada. La validación de
    /// negocio ocurre en la capa de servicio; aquí solo se persiste.
    /// </summary>
    Task<Result<bool, BaseError>> UpdateCityAsync(RoadshowCity city, CancellationToken cancellationToken);
}
