using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Participants;

/// <summary>
/// Quién está en un evento: la lista de participantes y, por debajo, la unión de la que cuelgan
/// el resumen (spec <c>07</c> §2) y el CSV (§4).
/// </summary>
public interface IParticipantService
{
    /// <summary>
    /// Todas las filas del evento que pasan el filtro, ya unidas y en el orden de la lista. Es la
    /// costura que reutilizan el resumen y el CSV, para que haya una sola unión y no tres que se
    /// separen.
    /// </summary>
    Task<Result<IReadOnlyList<Participant>, BaseError>> ListAsync(
        Guid eventId,
        ParticipantFilter filter,
        CancellationToken cancellationToken);

    /// <summary>Una página de <see cref="ListAsync"/>, topada en <see cref="MaxPageSize"/>.</summary>
    Task<Result<PagedResult<Participant>, BaseError>> ListPageAsync(
        Guid eventId,
        ParticipantFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una página de los destinatarios de una campaña, buscada por su id y no por su evento, porque
    /// un mensaje directo no tiene evento (PGTI-178). Son las mismas filas que
    /// <see cref="ListPageAsync"/> con <see cref="ParticipantFilter.CampaignId"/>: la misma unión, el
    /// mismo orden. <c>None</c> si la campaña no existe.
    /// </summary>
    Task<Result<Maybe<PagedResult<Participant>>, BaseError>> ListCampaignRecipientsPageAsync(
        Guid campaignId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// El tope de siempre. Subirlo para este endpoint se descartó (§3.3): esa presión es la que
    /// alivia el CSV, y aliviarla dos veces deja dos formas de hacer lo mismo.
    /// </summary>
    public const int MaxPageSize = 100;
}
