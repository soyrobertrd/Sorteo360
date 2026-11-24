using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Events;

/// <summary>
/// Contrato de lógica de negocio para el agregado Evento.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Crea un nuevo evento junto con su ciudad inicial.
    /// Devuelve el identificador del evento creado, o un error de infraestructura.
    /// </summary>
    Task<Result<Guid, BaseError>> CreateAsync(CreateEventDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene un evento con sus ciudades por su identificador.
    /// Devuelve <see cref="Maybe{T}.None"/> cuando el evento no existe.
    /// Devuelve un error de infraestructura si la consulta falla.
    /// </summary>
    Task<Result<Maybe<EventWithCities>, BaseError>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Inicia un evento: transición manual <c>planning → in_progress</c>. Devuelve el evento
    /// actualizado con sus ciudades, o <see cref="Maybe{T}.None"/> si el evento no existe.
    /// Devuelve un error <see cref="ErrorKind.Conflict"/> si el evento no está en planning.
    /// </summary>
    Task<Result<Maybe<EventWithCities>, BaseError>> StartAsync(Guid id, Guid startedById, CancellationToken cancellationToken);

    /// <summary>
    /// Cancela un evento desde <c>planning</c> o <c>in_progress</c>, persistiendo la razón de
    /// cancelación, y cierra en cascada (soft-cancel) las cotizaciones no terminales del evento.
    /// Devuelve el evento actualizado con sus ciudades, o <see cref="Maybe{T}.None"/> si el
    /// evento no existe. Devuelve un error <see cref="ErrorKind.Conflict"/> si el evento ya
    /// está en un estado terminal (cerrado o cancelado).
    /// </summary>
    Task<Result<Maybe<EventWithCities>, BaseError>> CancelAsync(Guid id, string reason, Guid cancelledById, CancellationToken cancellationToken);

    /// <summary>
    /// Actualiza los campos mutables de un evento (nombre, fechas, OPT, idioma, URL externa,
    /// cantidad de invitados). Devuelve el evento actualizado con sus ciudades, o
    /// <see cref="Maybe{T}.None"/> si el evento no existe. Devuelve un error
    /// <see cref="ErrorKind.Conflict"/> si el evento está en un estado terminal.
    /// </summary>
    Task<Result<Maybe<EventWithCities>, BaseError>> UpdateAsync(UpdateEventDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Cierra un evento: calcula su costo total final a partir de las cotizaciones aprobadas,
    /// lo materializa en el evento y fija su estado en <c>closed</c>. Devuelve el evento
    /// actualizado con sus ciudades, o <see cref="Maybe{T}.None"/> si el evento no existe.
    /// Devuelve un error <see cref="ErrorKind.Conflict"/> si el evento no está en
    /// <c>in_progress</c> (solo un evento iniciado puede cerrarse), o si el total no puede
    /// calcularse (sin cotizaciones, alguna en borrador, falta un monto final, o se mezclan
    /// monedas).
    /// </summary>
    Task<Result<Maybe<EventWithCities>, BaseError>> CloseAsync(Guid id, Guid closedById, CancellationToken cancellationToken);

    /// <summary>
    /// Lista eventos de forma paginada aplicando los filtros opcionales de <paramref nombre="filter"/>.
    /// Devuelve la página de eventos con su portada y el total de coincidencias, o un error de
    /// infraestructura si la consulta falla.
    /// </summary>
    Task<Result<PagedResult<EventWithCover>, BaseError>> ListAsync(ListEventsDto filter, CancellationToken cancellationToken);

    /// <summary>
    /// Lista los eventos de la puerta (spec <c>convocatorias/10</c> §4): los no cancelados con
    /// <c>StartDate − 1 ≤ hoy ≤ EndDate + 1</c>, hoy en UTC, públicos y privados por igual, con sus
    /// ciudades y ordenados por fecha de inicio y nombre.
    /// </summary>
    Task<Result<IReadOnlyList<DoorEvent>, BaseError>> ListDoorAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Actualiza los campos mutables de una ciudad (nombre, pais, fecha, display order).
    /// </summary>
    /// <remarks>
    /// Devuelve la ciudad actualizada, o <see cref="Maybe{T}.None"/> si el evento o la
    /// ciudad no existen. Devuelve un error <see cref="ErrorKind.Conflict"/> si el evento
    /// está en un estado terminal, sea del tipo que sea.
    /// </remarks>
    Task<Result<Maybe<RoadshowCity>, BaseError>> UpdateCityAsync(UpdateCityDto dto, CancellationToken cancellationToken);
}
