using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Core.Files;

public sealed class UploadPolicy : IUploadPolicy
{
    /// <summary>
    /// El tope de una firma: muy por encima de cualquier lienzo real y muy por debajo del general. Es
    /// una regla de este dueño y no un ajuste de operación, así que no es configurable (spec 03 §3.2).
    /// </summary>
    public const long MaxSignatureBytes = 1024 * 1024;

    private readonly UploadLimits _limits;

    public UploadPolicy(UploadLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
    }

    /// <inheritdoc />
    public async Task<Result<FileSignature, BaseError>> InspectAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        CancellationToken cancellationToken)
    {
        return await InspectAsync(content, fileName, declaredContentType, _limits.MaxSizeBytes, cancellationToken);
    }

    /// <inheritdoc />
    public UnitResult<BaseError> CheckServiceSupplierPhotoCount(int currentCount)
    {
        return currentCount >= _limits.MaxPhotosPerServiceSupplier
            ? UnitResult.Failure(FileUploadErrors.ServiceSupplierPhotoLimitReached(_limits.MaxPhotosPerServiceSupplier))
            : UnitResult.Success<BaseError>();
    }

    /// <inheritdoc />
    public UnitResult<BaseError> CheckProposalAttachmentCount(int currentCount)
    {
        return currentCount >= _limits.MaxAttachmentsPerProposal
            ? UnitResult.Failure(FileUploadErrors.ProposalAttachmentLimitReached(_limits.MaxAttachmentsPerProposal))
            : UnitResult.Success<BaseError>();
    }

    /// <inheritdoc />
    public UnitResult<BaseError> CheckEventGalleryImageCount(int currentCount)
    {
        return currentCount >= _limits.MaxGalleryImagesPerEvent
            ? UnitResult.Failure(FileUploadErrors.EventGalleryImageLimitReached(_limits.MaxGalleryImagesPerEvent))
            : UnitResult.Success<BaseError>();
    }

    /// <inheritdoc />
    public async Task<Result<FileSignature, BaseError>> InspectImageAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        CancellationToken cancellationToken)
    {
        var inspected = await InspectAsync(content, fileName, declaredContentType, cancellationToken);

        if (inspected.IsFailure)
            return inspected;

        // El rechazo lleva el mismo código que el genérico: quien consume ya ramifica por él, y
        // partirlo en dos obligaría a tratar el mismo caso —estos bytes no sirven acá— de dos
        // maneras. Lo que cambia es el mensaje, que sí nombra lo que este endpoint admite.
        return FileSignatures.AcceptedImages.Contains(inspected.Value)
            ? inspected
            : Result.Failure<FileSignature, BaseError>(FileUploadErrors.ImageTypeNotAllowed());
    }

    /// <inheritdoc />
    public async Task<Result<FileSignature, BaseError>> InspectSignatureAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        CancellationToken cancellationToken)
    {
        var inspected = await InspectAsync(content, fileName, declaredContentType, MaxSignatureBytes, cancellationToken);

        if (inspected.IsFailure)
            return inspected;

        // Mismo código que el rechazo genérico, como en las imágenes: lo que cambia es el mensaje.
        return inspected.Value == FileSignatures.Png
            ? inspected
            : Result.Failure<FileSignature, BaseError>(FileUploadErrors.SignatureTypeNotAllowed());
    }

    private static async Task<Result<FileSignature, BaseError>> InspectAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        long maxSizeBytes,
        CancellationToken cancellationToken)
    {
        var inspected = await FileContentInspector.InspectAsync(
            content, fileName, declaredContentType, maxSizeBytes, cancellationToken);

        return inspected.IsSuccess
            ? Result.Success<FileSignature, BaseError>(inspected.Value)
            : Result.Failure<FileSignature, BaseError>(ToError(inspected.Error, maxSizeBytes));
    }

    /// <summary>
    /// Traduce el motivo de la plataforma al código de este servicio, que es con el que el
    /// cliente decide qué mostrar.
    /// </summary>
    private static BaseError ToError(FileRejection rejection, long maxSizeBytes) => rejection switch
    {
        FileRejection.TooLarge => FileUploadErrors.FileTooLarge(maxSizeBytes),
        FileRejection.HeicNotSupported => FileUploadErrors.HeicNotSupported(),
        FileRejection.TypeMismatch => FileUploadErrors.FileTypeMismatch(),
        FileRejection.TypeNotAllowed => FileUploadErrors.FileTypeNotAllowed(),
        _ => throw new ArgumentOutOfRangeException(nameof(rejection), rejection, null)
    };
}
