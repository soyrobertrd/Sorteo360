using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Infrastructure.Raffles;

/// <summary>
/// Ocupa el lugar de la lista de DO Travel Awards hasta que PGTI-115 traiga la carga, y PGTI-115
/// la reemplaza. Se difirió a propósito (enmienda del 2026-10-01 al ADR-0022).
/// <para>
/// Contesta siempre que no hay lista, y no una lista vacía: sin lista el paso se salta y el aviso
/// <c>awards_list_not_in_force</c> lo dice en voz alta; una lista vacía dejaría a todos afuera.
/// </para>
/// </summary>
public sealed class NoAwardsList : IAwardsMembership
{
    public Task<Maybe<AwardsList>> GetInForceAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Maybe<AwardsList>.None);
}
