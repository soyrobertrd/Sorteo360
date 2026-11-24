using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Uploads;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>
/// Las dos tablas de una carga de destinatarios. Como en <see cref="ICampaignRepository"/>, la
/// guarda de borrador vive acá y no en el endpoint: se comprueba <b>dentro</b> de la transacción
/// que escribe, así que una campaña que deja de ser borrador mientras la petición está en vuelo
/// no se cuela.
/// <para>
/// Es la misma guarda que hace de solo lectura una campaña en <c>sending</c>. Desde que se
/// retiró la aprobación, <c>draft</c> es el único estado editable, así que esa guarda es la regla
/// entera y no un caso de ella (spec <c>05</c> §1).
/// </para>
/// </summary>
public interface ICampaignUploadRepository
{
    /// <summary>
    /// Lo que hace falta saber de la campaña <b>antes</b> de gastar una lectura del CRM por fila:
    /// que sigue siendo un borrador, y quién ya es destinatario. Nada si ese id no existe.
    /// </summary>
    Task<Result<Maybe<UploadStagingContext>, BaseError>> GetStagingContextAsync(
        Guid campaignId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Escribe el lote revisado y sus filas, <b>reemplazando</b> el lote sin confirmar que
    /// hubiera. Nada si ese id de campaña no existe.
    /// </summary>
    Task<Result<Maybe<CampaignUploadBatch>, BaseError>> StageAsync(
        StageRecipientUploadDto dto,
        CancellationToken cancellationToken);

    /// <summary>
    /// El lote sin confirmar de una campaña con sus filas, que es lo que recorre la
    /// confirmación. Nada si la campaña no existe o no tiene ninguno.
    /// </summary>
    Task<Result<Maybe<PendingUpload>, BaseError>> GetPendingAsync(
        Guid campaignId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Escribe el efecto de una fila y la marca aplicada, en una sola transacción: o las dos
    /// cosas o ninguna, que es lo que hace que el lote sea un cursor fiable.
    /// <para>
    /// Es idempotente sobre las tablas de la campaña: un contacto que ya es destinatario no se
    /// reescribe, y uno que estaba excluido y ahora entra pierde su exclusión, igual que en la
    /// selección. Sin eso, un contacto excluido una vez no podría entrar nunca más.
    /// </para>
    /// </summary>
    Task<UnitResult<BaseError>> ApplyRowAsync(ApplyUploadRowDto dto, CancellationToken cancellationToken);

    /// <summary>
    /// Cierra el lote: sella quién confirmó y cuándo, <b>recalcula las cuentas</b> desde el cubo
    /// final de cada fila —un 409 pudo mover alguna— y borra las filas, que es donde está el
    /// bulto. El lote se queda para siempre.
    /// </summary>
    Task<Result<Maybe<CampaignUploadBatch>, BaseError>> CompleteAsync(
        Guid campaignId,
        Guid confirmedById,
        CancellationToken cancellationToken);
}

/// <summary>
/// Lo que la revisión necesita saber de la campaña antes de empezar.
/// </summary>
/// <param name="RecipientContactIds">
/// Quién ya es destinatario. Es lo que decide el cubo <c>already_a_recipient</c>, y hace falta
/// entero antes de clasificar porque ese cubo gana a <c>suppressed</c>.
/// </param>
public sealed record UploadStagingContext(IReadOnlySet<Guid> RecipientContactIds);

/// <summary>El lote sin confirmar y sus filas, tal como las recorre la confirmación.</summary>
/// <param name="Batch">El lote.</param>
/// <param name="Rows">Sus filas, en el orden del archivo.</param>
public sealed record PendingUpload(CampaignUploadBatch Batch, IReadOnlyList<CampaignUploadRow> Rows);
