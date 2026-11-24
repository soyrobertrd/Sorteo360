using Sorteo360.Events.Core.Files;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Un premio con su estado y sus sorteos, del más nuevo al más viejo: lo que devuelve el sorteo, en
/// la misma forma en que la lectura del sorteo muestra cada premio (spec 02 §2), con el archivo del
/// logo de su patrocinador si tiene uno.
/// </summary>
public sealed record PrizeWithDraws(Prize Prize, IReadOnlyList<PrizeDraw> Draws, StoredFile? SponsorLogo = null)
{
    public string State => PrizeState.Of(Draws);
}
