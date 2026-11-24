using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// La entrega de una campaña, que es lo que corre <b>después</b> del 202 y sin navegador detrás
/// (spec <c>04</c> §1 pasos 7 y 8).
/// </summary>
public interface ICampaignHandoffService
{
    /// <summary>
    /// Entrega lo que le quede pendiente a esta campaña y la deja en <c>sent</c> si no queda
    /// ninguna fila pendiente. Un fallo deja las filas sin entregar en <c>pending</c> y la campaña
    /// en <c>sending</c>, que es de donde una persona la reanuda (spec <c>04</c> §5).
    /// </summary>
    /// <param name="campaignId">La campaña que salió del canal.</param>
    /// <param name="cancellationToken">
    /// El del ciclo de vida del host, nunca el de una petición (ADR-0018).
    /// </param>
    Task<UnitResult<BaseError>> HandOffAsync(Guid campaignId, CancellationToken cancellationToken);
}
