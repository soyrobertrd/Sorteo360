using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Handoff;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Handoff;

/// <summary>
/// El canal en memoria por el que el lanzamiento le pasa una campaña al worker (spec <c>04</c>
/// §1.1, ADR-0018).
/// </summary>
public sealed class CampaignHandoffQueueTests
{
    private readonly CampaignHandoffQueue _queue = new();

    [Fact]
    public async Task DequeueAll_ReturnsTheEnqueuedCampaign_WhenOneWasEnqueued()
    {
        var campaignId = Guid.NewGuid();

        _queue.Enqueue(campaignId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var drained = new List<Guid>();

        await foreach (var id in _queue.DequeueAllAsync(cts.Token))
        {
            drained.Add(id);
            break;
        }

        drained.ShouldBe([campaignId]);
    }

    /// <summary>
    /// Un canal vacío no entrega nada y tampoco termina: es lo que hace que el worker no sea un
    /// sondeo. Sin esto habría que elegir un intervalo, y cualquiera que se eligiera sería o una
    /// espera que se nota o una consulta que no hacía falta.
    /// </summary>
    [Fact]
    public async Task DequeueAll_ReturnsNothingAndKeepsWaiting_WhenTheQueueIsEmpty()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var drain = Task.Run(
            async () =>
            {
                await foreach (var _ in _queue.DequeueAllAsync(cts.Token))
                {
                    return true;
                }

                return false;
            },
            CancellationToken.None);

        await Should.ThrowAsync<OperationCanceledException>(drain);
    }

    /// <summary>
    /// <b>La guarda del ticket, escrita como firma y no como costumbre.</b> Quien encola es el
    /// manejador del lanzamiento, y un <c>CancellationToken</c> declarado ahí se enlaza a
    /// <c>RequestAborted</c>: si <c>Enqueue</c> lo admitiera, el envío moriría al cerrarse la
    /// pestaña. Hay doce manejadores en <c>ServiceSupplierEndpoints.cs</c> que declaran ese
    /// parámetro, así que la costumbre existe y la firma es lo único que la para.
    /// </summary>
    [Fact]
    public void EnqueueSignature_ReturnsOnlyTheCampaignIdParameter_WhenReflected()
    {
        var enqueue = typeof(ICampaignHandoffQueue).GetMethod(nameof(ICampaignHandoffQueue.Enqueue));

        enqueue.ShouldNotBeNull();
        enqueue.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe([typeof(Guid)]);
    }
}
