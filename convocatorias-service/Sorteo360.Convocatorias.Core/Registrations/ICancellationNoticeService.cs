using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// El aviso de cancelación (spec <c>06</c> §4.7): cuando un evento se cancela, cada persona inscrita
/// recibe un correo genérico que se lo dice.
/// <para>
/// <b>Es una difusión transaccional, no una campaña.</b> La lista de una campaña la elige una
/// persona desde el CRM; la de esta sale de filas que ya tenemos. Así que no tiene borrador, ni
/// elección de plantilla, ni lanzamiento, ni enlace de baja, y no entra en el conteo de invitados
/// de <c>07</c>.
/// </para>
/// <para>
/// <b>Se llama dos veces por diseño.</b> events-service nunca llama a convocatorias, así que el
/// diálogo de cancelar del frontend lo llama justo después de cancelar el evento, y el cubo de
/// <c>07</c> §6.5 lo vuelve a correr para cuando esa segunda llamada no llegó.
/// <c>cancellation_notified_at</c> es lo que hace que la segunda vez no le escriba a nadie dos
/// veces.
/// </para>
/// </summary>
public interface ICancellationNoticeService
{
    /// <summary>
    /// Le avisa a cada inscripción del evento que todavía no fue avisada. Se niega si events-service
    /// no dice que el evento está cancelado, y si no se le puede preguntar.
    /// </summary>
    /// <param name="eventId">El evento cancelado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<CancellationNoticeOutcome, BaseError>> NotifyAsync(
        Guid eventId,
        CancellationToken cancellationToken);
}

/// <summary>Lo que hizo una pasada del aviso.</summary>
/// <param name="Notified">A cuántas personas les salió el correo en esta pasada.</param>
/// <param name="Failed">
/// A cuántas no. Quedan sin sello, así que el cubo de <c>07</c> §6.5 las sigue mostrando y la
/// próxima pasada las vuelve a intentar.
/// </param>
public sealed record CancellationNoticeOutcome(int Notified, int Failed);
