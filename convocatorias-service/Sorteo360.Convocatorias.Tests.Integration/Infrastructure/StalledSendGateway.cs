using System.Diagnostics;
using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// El transporte que nunca contesta. Es el de por omisión en <see cref="ConvocatoriasApiDbFactory"/>:
/// deja las filas de una campaña lanzada exactamente donde el <c>202</c> las dejó, para que una
/// clase que afirma sobre eso no compita contra el worker.
/// <para>
/// No devuelve un fallo, que sería otra cosa: un lote fallido se anota como tal y la campaña
/// termina. Esto se queda esperando, que es lo que hace notification-service cuando tarda.
/// </para>
/// </summary>
public sealed class StalledSendGateway : ICampaignSendGateway
{
    /// <inheritdoc />
    public async Task<Result<SendBatchResult, BaseError>> SendAsync(
        SendBatchRequest request,
        CancellationToken cancellationToken)
    {
        // Espera al token del host, y nada más. Al pararse suelta la espera con una cancelación,
        // que es lo que el worker ya sabe tratar, así que el host cierra sin colgarse.
        await Task.Delay(Timeout.Infinite, cancellationToken);

        throw new UnreachableException();
    }
}
