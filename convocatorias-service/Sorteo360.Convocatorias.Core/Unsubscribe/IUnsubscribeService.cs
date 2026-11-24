using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Unsubscribe;

/// <summary>
/// La baja del portal: un token, una fila de destinatario, y un opt-out empujado al CRM
/// (spec <c>06</c> §6). <b>La puerta es convocatorias, el estado es el CRM.</b>
/// </summary>
public interface IUnsubscribeService
{
    /// <summary>
    /// Da de baja a quien lleva ese token y devuelve su dirección enmascarada. Empuja el opt-out
    /// al CRM; si el CRM no está, la baja igual triunfa del lado de la persona y la fila queda
    /// pendiente de sincronizar, que el próximo lanzamiento vacía (spec <c>06</c> §6, espeja §4.3).
    /// Un token que no casa ninguna fila sale por <c>404</c>.
    /// </summary>
    Task<Result<string, BaseError>> UnsubscribeAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Reintenta a mano empujar al CRM una baja que quedó pendiente (spec <c>07</c> §6.2),
    /// <b>por el mismo camino que la baja pública</b>. Una ya sincronizada vuelve tal cual, sin
    /// llamar al CRM; con el CRM todavía caído sale <c>503 CRM_UNAVAILABLE</c> y la fila sigue
    /// pendiente; quien nunca se dio de baja sale por <c>409 RECIPIENT_NOT_UNSUBSCRIBED</c>.
    /// <see cref="Maybe{T}.None"/> si ese id no existe.
    /// </summary>
    Task<Result<Maybe<RecipientUnsubscribe>, BaseError>> RetrySyncAsync(
        Guid recipientId,
        CancellationToken cancellationToken);
}
