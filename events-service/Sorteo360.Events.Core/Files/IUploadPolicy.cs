using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Core.Files;

/// <summary>
/// Las reglas de lo que se admite subir, en un solo sitio: qué tamaño, qué clase de archivo, y
/// cuántos puede acumular un dueño.
/// <para>
/// Todas se comprueban <b>antes</b> de escribir un solo byte, así que un archivo rechazado no
/// deja nada en el almacenamiento.
/// </para>
/// </summary>
public interface IUploadPolicy
{
    /// <summary>
    /// Decide si un archivo se admite y de qué clase es realmente. Lo que devuelve es lo que
    /// dicen sus bytes: el tipo declarado y la extensión solo se usan para detectar que
    /// contradicen al contenido, nunca para decidir.
    /// <para>
    /// El contenido debe poder rebobinarse y se deja en la posición en que llegó, porque quien
    /// llama lo entrega después al almacenamiento.
    /// </para>
    /// </summary>
    /// <param name="content">Contenido a inspeccionar; debe admitir búsqueda.</param>
    /// <param name="fileName">Nombre original, del que sale la extensión declarada.</param>
    /// <param name="declaredContentType">Tipo declarado por quien sube el archivo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Result<FileSignature, BaseError>> InspectAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Comprueba si una ficha de proveedor admite una foto más, dadas las que ya tiene.
    /// </summary>
    UnitResult<BaseError> CheckServiceSupplierPhotoCount(int currentCount);

    /// <summary>
    /// Comprueba si una propuesta admite un adjunto más, dados los que ya tiene. Es un cupo
    /// propio y no comparte número con el de las fotos: lo que acota es otra cosa.
    /// </summary>
    UnitResult<BaseError> CheckProposalAttachmentCount(int currentCount);

    /// <summary>
    /// Comprueba si un evento admite una foto de galería más, dadas las que ya tiene. La portada
    /// no entra en esta cuenta: es única por evento y no compite con la galería por el cupo.
    /// </summary>
    UnitResult<BaseError> CheckEventGalleryImageCount(int currentCount);

    /// <summary>
    /// Lo mismo que <see cref="InspectAsync"/>, pero además exige que los bytes sean de una
    /// imagen. Un PDF pasa las reglas generales y no sirve de portada ni de galería: lo que se
    /// sube acá se muestra, no se descarga.
    /// </summary>
    Task<Result<FileSignature, BaseError>> InspectImageAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// La firma de una entrega de premio (spec 03 §3.2): las reglas generales con un tope de
    /// <see cref="UploadPolicy.MaxSignatureBytes"/> en vez del general, y además solo PNG, que es lo que
    /// emite el panel de firma del navegador. Un JPEG o un PDF de una firma es una foto de un papel,
    /// que es otro acto.
    /// </summary>
    Task<Result<FileSignature, BaseError>> InspectSignatureAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        CancellationToken cancellationToken);
}
