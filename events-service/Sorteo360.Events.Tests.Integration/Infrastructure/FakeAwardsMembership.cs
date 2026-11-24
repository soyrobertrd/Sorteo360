using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// La lista de DO Travel Awards en vigor, programada por el test. Es una sola para toda la plataforma,
/// así que un test que la pone la retira al terminar. Sin programar contesta como el marcador
/// registrado: ninguna en vigor.
/// </summary>
public sealed class FakeAwardsMembership : IAwardsMembership
{
    private volatile AwardsList? _inForce;

    /// <summary>Desde ahora está en vigor una lista con estos miembros. Devuelve su id.</summary>
    public Guid SetInForce(params string[] members)
    {
        var list = new AwardsList(Guid.NewGuid(), new HashSet<string>(members, StringComparer.Ordinal));
        _inForce = list;
        return list.BatchId;
    }

    /// <summary>Desde ahora no hay ninguna lista en vigor.</summary>
    public void ClearInForce() => _inForce = null;

    public Task<Maybe<AwardsList>> GetInForceAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_inForce is { } list ? Maybe.From(list) : Maybe<AwardsList>.None);
}
