using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Participants;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>Las dos tablas de las que sale la lista de participantes (spec <c>07</c> §3).</summary>
public interface IParticipantRepository
{
    /// <summary>
    /// Los destinatarios y las inscripciones del evento, sin unir. Sin <paramref name="campaignId"/>
    /// entran los destinatarios de toda campaña que no sea borrador; con él, los de esa campaña y
    /// ningún otro, borrador incluido (§2, §3.3). Las inscripciones entran todas.
    /// </summary>
    Task<Result<ParticipantSources, BaseError>> ReadSourcesAsync(
        Guid eventId,
        Guid? campaignId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lo mismo que <see cref="ReadSourcesAsync"/> con el <c>campaignId</c> puesto, pero buscando el
    /// evento por la campaña: los destinatarios de esa campaña, borrador incluido, y las
    /// inscripciones de su evento. Un mensaje directo no tiene evento, así que no trae ninguna
    /// (PGTI-178). <c>None</c> si la campaña no existe.
    /// </summary>
    Task<Result<Maybe<ParticipantSources>, BaseError>> ReadCampaignSourcesAsync(
        Guid campaignId,
        CancellationToken cancellationToken);
}
