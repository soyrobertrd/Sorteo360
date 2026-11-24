using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Events;

/// <summary>
/// events-service visto desde el dominio: las dos lecturas que este servicio hace de un evento.
/// Ninguna firma nombra transporte, que es lo que permite sustituirlo en los tests de dominio.
/// <para>
/// <b>Acá tampoco hay escalera de degradación</b>, igual que en <c>ICrmContactDirectory</c> y por
/// la misma razón: un evento que no se pudo leer no se puede rellenar con nada. Un fallo sale
/// como <c>503 EVENTS_UNAVAILABLE</c> y nadie recibe un correo con un hueco (spec <c>01</c> §6).
/// </para>
/// <para>
/// <b>Y no hay caché.</b> Una lista pública cacheada es una decisión de visibilidad cacheada, que
/// es como un evento privado sigue viéndose cinco minutos después de que alguien lo escondió.
/// </para>
/// </summary>
public interface IEventDirectory
{
    /// <summary>
    /// Un evento por id, cualquiera sea su visibilidad y su estado. Quien llama decide qué hacer
    /// con lo que trae: el portal lo esconde si no es público, y el lanzamiento lo lee para armar
    /// el cuerpo del correo.
    /// <para>
    /// <see cref="Maybe{T}.None"/> es "events-service no conoce ese id", que no es una avería.
    /// </para>
    /// </summary>
    Task<Result<Maybe<EventSummary>, BaseError>> GetEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Los eventos públicos que todavía no terminaron, que es lo que alimenta el listado del
    /// portal. El filtro lo aplica events-service —es su dato— y se repite acá antes de publicar:
    /// lo que cuesta una condición de más es lo que impide que un cambio de semántica del otro
    /// lado exponga un evento privado.
    /// </summary>
    Task<Result<IReadOnlyList<EventSummary>, BaseError>> ListPublicEventsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Los eventos cancelados, que es lo único que la reconciliación no puede saber sola (spec
    /// <c>07</c> §6.5). Se lee cuando alguien abre la pantalla, nunca con un temporizador: el
    /// sondeo fue rechazado en <c>06</c> §4.7.
    /// </summary>
    Task<Result<IReadOnlyList<CancelledEvent>, BaseError>> ListCancelledEventsAsync(CancellationToken cancellationToken);
}
