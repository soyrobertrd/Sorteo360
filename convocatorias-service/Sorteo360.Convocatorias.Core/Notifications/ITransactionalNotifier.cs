using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Notifications;

/// <summary>
/// Por dónde sale un correo transaccional (spec <c>04</c> §4). Es la costura, no el transporte:
/// detrás van el stub con su tabla y el cliente real de notification-service, y cambiar de uno a
/// otro no cambia nada de este lado. Es la costura hermana de <c>ICampaignSendGateway</c>, con la
/// misma elección de transporte, para lo que la spec <c>04</c> §2 llama transaccional en vez de
/// campaña.
/// <para>
/// <b>Un fallo es del lote entero, y del lote entero es una sola persona.</b> Un
/// <see cref="Result"/> fallido significa que el correo no salió; no hay filas que dejar
/// <c>pending</c> ni un cursor que reanude, así que quien llama decide qué hacer con el fallo. En
/// la confirmación de una inscripción, lo registra y sigue: la inscripción ya existe y el QR está
/// en la pantalla (spec <c>06</c> §4.4).
/// </para>
/// </summary>
public interface ITransactionalNotifier
{
    /// <summary>Entrega un correo transaccional a un destinatario.</summary>
    /// <param name="notification">El correo, con sus marcadores sin sustituir.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<UnitResult<BaseError>> SendAsync(
        TransactionalNotification notification,
        CancellationToken cancellationToken);
}
