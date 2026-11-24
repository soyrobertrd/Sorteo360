using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Handoff;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Handoff;
using Sorteo360.Platform.Errors;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Handoff;

/// <summary>
/// El worker a pelo, sin host ni base de datos, para cobrar la única cosa que un test contra la
/// API no puede cobrar: <b>de quién es el token que baja hasta la entrega</b>.
/// <para>
/// Un <c>CancellationToken</c> declarado en un manejador de Minimal API es
/// <c>HttpContext.RequestAborted</c> y muere con la petición; el del worker es el del host y muere
/// cuando el host se para. Acá se demuestra por observación: el mismo token que recibió la entrega
/// sigue vivo mientras el worker corre y se cancela exactamente al pararlo.
/// </para>
/// </summary>
public sealed class CampaignHandoffWorkerTests
{
    [Fact]
    public async Task Worker_ReturnsAHandoffTokenCancelledOnlyByTheHost_WhenStopped()
    {
        var handoff = new TokenCapturingHandoff();
        var queue = new CampaignHandoffQueue();
        var worker = new CampaignHandoffWorker(queue, ScopeFactoryFor(handoff), NullLogger<CampaignHandoffWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        queue.Enqueue(Guid.NewGuid());

        var token = await handoff.Captured.WaitAsync(TimeSpan.FromSeconds(5));

        // Mientras el worker corre, el token está vivo. Si fuera el de la petición que lanzó, ya
        // estaría cancelado: esa petición terminó hace rato.
        token.IsCancellationRequested.ShouldBeFalse();
        token.CanBeCanceled.ShouldBeTrue();

        await worker.StopAsync(CancellationToken.None);

        // Y se cancela al parar el host, que es el ciclo de vida al que tiene que estar atado.
        token.IsCancellationRequested.ShouldBeTrue();
    }

    private static IServiceScopeFactory ScopeFactoryFor(ICampaignHandoffService handoff)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ICampaignSendGateway>(new RecordingSendGateway());
        services.AddSingleton(handoff);

        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    /// <summary>Se queda con el token con el que se la llamó y no hace nada más.</summary>
    private sealed class TokenCapturingHandoff : ICampaignHandoffService
    {
        private readonly TaskCompletionSource<CancellationToken> _captured =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CancellationToken> Captured => _captured.Task;

        public Task<UnitResult<BaseError>> HandOffAsync(Guid campaignId, CancellationToken cancellationToken)
        {
            _captured.TrySetResult(cancellationToken);

            return Task.FromResult(UnitResult.Success<BaseError>());
        }
    }
}
