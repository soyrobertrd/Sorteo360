using System.Threading.Channels;
using Sorteo360.Convocatorias.Core.Campaigns;

namespace Sorteo360.Convocatorias.Infrastructure.Handoff;

/// <inheritdoc cref="ICampaignHandoffQueue" />
public sealed class CampaignHandoffQueue : ICampaignHandoffQueue
{
    /// <summary>
    /// Sin tope: lo que cabe acá son ids de campaña, uno por lanzamiento, y una cola que se
    /// llenara dejaría a quien lanza esperando dentro de la petición, que es exactamente lo que
    /// el 202 quita de en medio. Un solo lector, porque el worker es uno.
    /// </summary>
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    /// <inheritdoc />
    public void Enqueue(Guid campaignId) => _channel.Writer.TryWrite(campaignId);

    /// <inheritdoc />
    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
