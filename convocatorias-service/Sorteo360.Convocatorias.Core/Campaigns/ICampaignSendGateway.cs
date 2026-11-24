using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Por dónde sale un lote de correos (spec <c>04</c> §4). Es la costura, no el transporte: detrás
/// van el stub con su tabla y el cliente real de notification-service, y cambiar de uno a otro no
/// cambia nada de este lado.
/// <para>
/// <b>Un fallo acá no es un rechazo por persona.</b> Un <see cref="Result"/> fallido significa que
/// el lote entero no se entregó y sus filas se quedan <c>pending</c>, que es lo que permite que
/// alguien reanude por la cola sin volver a escribirle a quien ya recibió (spec <c>04</c> §2.1 y
/// §5). El rechazo de una persona concreta viaja dentro de un resultado correcto, como un
/// <see cref="RecipientHandoffOutcome"/> fallido.
/// </para>
/// </summary>
public interface ICampaignSendGateway
{
    /// <summary>Entrega un lote y devuelve qué pasó con cada destinatario.</summary>
    /// <param name="request">El lote, ya troceado por quien llama (spec <c>04</c> §2.2).</param>
    /// <param name="cancellationToken">
    /// El del ciclo de vida del host. Nunca el de una petición: ver <see cref="ICampaignHandoffQueue"/>.
    /// </param>
    Task<Result<SendBatchResult, BaseError>> SendAsync(
        SendBatchRequest request,
        CancellationToken cancellationToken);
}
