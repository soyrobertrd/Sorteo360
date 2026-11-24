using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Subir una hoja a una campaña en borrador y, en una segunda llamada, aplicarla.
/// <para>
/// Son dos actos y no uno a propósito: crear los contactos en silencio significa que una columna
/// con una errata mete basura en el sistema de registro, y la pantalla de revisión es donde de
/// todos modos tienen que salir los avisos de supresión y de duplicado (spec <c>05</c>).
/// </para>
/// <para>
/// <b>Una carga es una segunda fuente de destinatarios sobre una campaña en borrador</b>, al
/// lado de la selección del CRM, y no una importación suelta de contactos. Una importación suelta
/// no tendría dónde poner una fila encontrada y suprimida —<c>campaign_exclusion</c> es por
/// campaña— y crearía contactos sueltos y sin clasificación, que es justo lo que el único
/// mecanismo de selección que hay en el producto no sabe encontrar.
/// </para>
/// </summary>
public interface IRecipientUploadService
{
    /// <summary>
    /// Lee el archivo, decide qué pasa con cada fila y lo deja pendiente de confirmar.
    /// <see cref="Maybe{T}.None"/> si esa campaña no existe.
    /// <para>
    /// <b>Un fallo de fila nunca impide revisar.</b> Un archivo donde todas las filas están mal
    /// se revisa igual, con cero filas confirmables: un 400 no le diría a nadie que se equivocó
    /// de columnas.
    /// </para>
    /// </summary>
    Task<Result<Maybe<PendingUpload>, BaseError>> StageAsync(
        StageUploadRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Aplica la carga pendiente: crea en el CRM lo que no estaba, enlaza lo que sí, escribe una
    /// exclusión por cada fila suprimida y no toca las que ya eran destinatarias.
    /// <see cref="Maybe{T}.None"/> si esa campaña no existe.
    /// <para>
    /// <b>Sincrónica, reanudable e idempotente por fila.</b> Un corte del CRM la para y deja el
    /// lote vivo y a medias; volver a llamar salta todo lo ya aplicado y sigue. Lo que se creó en
    /// el CRM se queda creado: no tenemos autoridad para borrar a una persona de allí para
    /// limpiar un lote nuestro.
    /// </para>
    /// </summary>
    Task<Result<Maybe<CampaignUploadBatch>, BaseError>> ConfirmAsync(
        Guid campaignId,
        Guid confirmedById,
        CancellationToken cancellationToken);
}

/// <summary>El archivo que llegó, con quién lo mandó y a qué campaña.</summary>
/// <param name="CampaignId">La campaña, que tiene que estar en borrador.</param>
/// <param name="Content">El contenido; debe poder rebobinarse.</param>
/// <param name="FileName">El nombre original.</param>
/// <param name="DeclaredContentType">Lo que declaró el formulario, que no decide nada por sí solo.</param>
/// <param name="UploadedById">Quién subió.</param>
public sealed record StageUploadRequest(
    Guid CampaignId,
    Stream Content,
    string FileName,
    string DeclaredContentType,
    Guid UploadedById);
