using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Files;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Files;

/// <summary>
/// Con qué código se entera quien lo intentó, y cuántos archivos acumula cada dueño. Cómo se
/// inspeccionan los bytes es de la plataforma y se prueba allá (<c>FileContentInspectorTests</c>);
/// acá queda un caso por motivo, para fijar el código que este servicio responde a cada uno.
/// </summary>
public sealed class UploadPolicyTests
{
    private readonly UploadPolicy _sut = new(new UploadLimits());

    #region What is accepted

    [Fact]
    public async Task InspectAsync_ReturnsTheJpegKind_WhenTheBytesAreJpeg()
    {
        var result = await InspectAsync(SampleFiles.Jpeg(), "salon.jpg", "image/jpeg");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe("image/jpeg");
    }

    #endregion

    #region What is refused

    [Fact]
    public async Task InspectAsync_ReturnsFileTooLarge_WhenTheFileExceedsTheLimit()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxSizeBytes = 32 });
        using var content = new MemoryStream([.. SampleFiles.Jpeg(), .. new byte[64]]);

        var result = await policy.InspectAsync(content, "salon.jpg", "image/jpeg", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_TOO_LARGE");
    }

    /// <summary>El límite es configurable: el mismo archivo pasa con el cupo puesto más alto.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsSuccess_WhenTheLimitIsRaised()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxSizeBytes = 1024 });
        using var content = new MemoryStream([.. SampleFiles.Jpeg(), .. new byte[64]]);

        var result = await policy.InspectAsync(content, "salon.jpg", "image/jpeg", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task InspectAsync_ReturnsTypeNotAllowed_WhenTheContentIsOfNoKnownKind()
    {
        var result = await InspectAsync(SampleFiles.Html(), "salon.jpg", "image/jpeg");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_TYPE_NOT_ALLOWED");
    }

    /// <summary>Excel es de los archivos de una empresa en el CRM; ninguna subida de eventos lo admite.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsTypeNotAllowed_WhenTheFileIsAWorkbook()
    {
        var result = await InspectAsync(SampleFiles.Xlsx(), "tarifas.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_TYPE_NOT_ALLOWED");
    }

    /// <summary>
    /// El caso que da nombre a todo esto: un PDF disfrazado de imagen. Se rechaza en vez de
    /// corregirse, porque el nombre es lo que después se muestra.
    /// </summary>
    [Fact]
    public async Task InspectAsync_ReturnsTypeMismatch_WhenTheContentContradictsTheDeclaredType()
    {
        var result = await InspectAsync(SampleFiles.Pdf(), "salon.png", "image/png");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_TYPE_MISMATCH");
    }

    [Fact]
    public async Task InspectAsync_ReturnsHeicNotSupported_WhenTheContentIsHeic()
    {
        var result = await InspectAsync(SampleFiles.Heic(), "IMG_0001.heic", "image/heic");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_TYPE_HEIC_NOT_SUPPORTED");
    }

    /// <summary>
    /// Es el rechazo que más gente va a ver, así que el mensaje tiene que decir qué cambiar y no
    /// solo que el formato no sirve.
    /// </summary>
    [Fact]
    public async Task InspectAsync_ReturnsAMessageExplainingTheCameraSetting_WhenTheContentIsHeic()
    {
        var result = await InspectAsync(SampleFiles.Heic(), "IMG_0001.heic", "image/heic");

        result.Error.Message.ShouldContain("Ajustes");
        result.Error.Message.ShouldContain("JPEG");
    }

    #endregion

    #region Guards

    [Fact]
    public void Constructor_Throws_WhenThereAreNoLimits()
    {
        Should.Throw<ArgumentNullException>(() => new UploadPolicy(null!));
    }

    #endregion

    #region CheckServiceSupplierPhotoCount

    [Fact]
    public void CheckServiceSupplierPhotoCount_ReturnsSuccess_WhenTheCountIsBelowTheLimit()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxPhotosPerServiceSupplier = 3 });

        policy.CheckServiceSupplierPhotoCount(2).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckServiceSupplierPhotoCount_ReturnsPhotoLimitReached_WhenTheLimitIsReached()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxPhotosPerServiceSupplier = 3 });

        var result = policy.CheckServiceSupplierPhotoCount(3);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("SUPPLIER_PHOTO_LIMIT_REACHED");
    }

    /// <summary>El cupo agotado se dice con el número, que es lo que hay que quitar para seguir.</summary>
    [Fact]
    public void CheckServiceSupplierPhotoCount_ReturnsAMessageWithTheLimit_WhenTheLimitIsReached()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxPhotosPerServiceSupplier = 3 });

        policy.CheckServiceSupplierPhotoCount(3).Error.Message.ShouldContain("3");
    }

    #endregion

    #region CheckProposalAttachmentCount

    [Fact]
    public void CheckProposalAttachmentCount_ReturnsSuccess_WhenTheCountIsBelowTheLimit()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxAttachmentsPerProposal = 2 });

        policy.CheckProposalAttachmentCount(1).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckProposalAttachmentCount_ReturnsAttachmentLimitReached_WhenTheLimitIsReached()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxAttachmentsPerProposal = 2 });

        var result = policy.CheckProposalAttachmentCount(2);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PROPOSAL_ATTACHMENT_LIMIT_REACHED");
    }

    /// <summary>El cupo agotado se dice con el número, que es lo que hay que quitar para seguir.</summary>
    [Fact]
    public void CheckProposalAttachmentCount_ReturnsAMessageWithTheLimit_WhenTheLimitIsReached()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxAttachmentsPerProposal = 2 });

        policy.CheckProposalAttachmentCount(2).Error.Message.ShouldContain("2");
    }

    /// <summary>
    /// Los dos cupos son independientes: una foto de proveedor y un adjunto de propuesta no
    /// compiten por el mismo número.
    /// </summary>
    [Fact]
    public void CheckProposalAttachmentCount_ReturnsSuccess_WhenOnlyThePhotoLimitIsExceeded()
    {
        var policy = new UploadPolicy(new UploadLimits
        {
            MaxPhotosPerServiceSupplier = 1,
            MaxAttachmentsPerProposal = 10
        });

        policy.CheckProposalAttachmentCount(5).IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region CheckEventGalleryImageCount

    [Fact]
    public void CheckEventGalleryImageCount_ReturnsSuccess_WhenTheCountIsBelowTheLimit()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxGalleryImagesPerEvent = 3 });

        policy.CheckEventGalleryImageCount(2).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CheckEventGalleryImageCount_ReturnsGalleryImageLimitReached_WhenTheLimitIsReached()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxGalleryImagesPerEvent = 3 });

        var result = policy.CheckEventGalleryImageCount(3);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_GALLERY_IMAGE_LIMIT_REACHED");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public void CheckEventGalleryImageCount_ReturnsAMessageWithTheLimit_WhenTheLimitIsReached()
    {
        var policy = new UploadPolicy(new UploadLimits { MaxGalleryImagesPerEvent = 3 });

        policy.CheckEventGalleryImageCount(3).Error.Message.ShouldContain("3");
    }

    /// <summary>
    /// El cupo de la galería no comparte número con los otros dos: son tres cosas distintas.
    /// </summary>
    [Fact]
    public void CheckEventGalleryImageCount_ReturnsSuccess_WhenOnlyTheOtherLimitsAreExceeded()
    {
        var policy = new UploadPolicy(new UploadLimits
        {
            MaxPhotosPerServiceSupplier = 1,
            MaxAttachmentsPerProposal = 1,
            MaxGalleryImagesPerEvent = 20
        });

        policy.CheckEventGalleryImageCount(5).IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region InspectImageAsync

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public async Task InspectImageAsync_ReturnsTheKind_WhenTheContentIsAnImage(string expected)
    {
        var (bytes, fileName) = expected switch
        {
            "image/png"  => (SampleFiles.Png(), "portada.png"),
            "image/webp" => (SampleFiles.Webp(), "portada.webp"),
            _            => (SampleFiles.Jpeg(), "portada.jpg")
        };

        var result = await InspectImageAsync(bytes, fileName, expected);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe(expected);
    }

    /// <summary>
    /// Un PDF pasa las reglas generales y aun así no sirve acá: lo que se sube a un evento se
    /// muestra, no se descarga.
    /// </summary>
    [Fact]
    public async Task InspectImageAsync_ReturnsTypeNotAllowed_WhenTheContentIsAPdf()
    {
        var result = await InspectImageAsync(SampleFiles.Pdf(), "portada.pdf", "application/pdf");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_TYPE_NOT_ALLOWED");
        result.Error.Kind.ShouldBe(ErrorKind.Invalid);
    }

    /// <summary>
    /// Las reglas generales siguen corriendo antes: HEIC se rechaza con su propio motivo y no
    /// como "no es una imagen", que es justamente lo que no le sirve a quien la tomó.
    /// </summary>
    [Fact]
    public async Task InspectImageAsync_ReturnsHeicNotSupported_WhenTheContentIsHeic()
    {
        var result = await InspectImageAsync(SampleFiles.Heic(), "portada.heic", "image/heic");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_TYPE_HEIC_NOT_SUPPORTED");
    }

    [Fact]
    public async Task InspectImageAsync_ReturnsTypeMismatch_WhenTheContentContradictsWhatWasDeclared()
    {
        var result = await InspectImageAsync(SampleFiles.Jpeg(), "portada.png", "image/png");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_TYPE_MISMATCH");
    }

    #endregion

    #region InspectSignatureAsync

    [Fact]
    public async Task InspectSignatureAsync_ReturnsPng_WhenTheContentIsAPngUnderOneMegabyte()
    {
        var bytes = PaddedPng(UploadPolicy.MaxSignatureBytes);

        var result = await InspectSignatureAsync(bytes, "firma.png", "image/png");

        result.Value.ShouldBe(FileSignatures.Png);
    }

    [Fact]
    public async Task InspectSignatureAsync_ReturnsFileTooLarge_WhenThePngIsOverOneMegabyte()
    {
        var bytes = PaddedPng(UploadPolicy.MaxSignatureBytes + 1);

        var result = await InspectSignatureAsync(bytes, "firma.png", "image/png");

        result.Error.Code.ShouldBe("FILE_TOO_LARGE");
        result.Error.Message.ShouldContain("1 MB");
    }

    [Theory]
    [InlineData("jpeg")]
    [InlineData("pdf")]
    [InlineData("webp")]
    public async Task InspectSignatureAsync_ReturnsTypeNotAllowed_WhenTheContentIsNotAPng(string kind)
    {
        var (bytes, fileName, declared) = kind switch
        {
            "jpeg" => (SampleFiles.Jpeg(), "firma.jpg", "image/jpeg"),
            "pdf"  => (SampleFiles.Pdf(), "firma.pdf", "application/pdf"),
            _      => (SampleFiles.Webp(), "firma.webp", "image/webp")
        };

        var result = await InspectSignatureAsync(bytes, fileName, declared);

        result.Error.Code.ShouldBe("FILE_TYPE_NOT_ALLOWED");
        result.Error.Kind.ShouldBe(ErrorKind.Invalid);
    }

    [Theory]
    [InlineData("firma.png", "image/jpeg")]
    [InlineData("firma.jpg", "image/png")]
    [InlineData("firma.png", "image/png")]
    public async Task InspectSignatureAsync_ReturnsTypeMismatch_WhenAJpegClaimsToBeAPng(string fileName, string declared)
    {
        var result = await InspectSignatureAsync(SampleFiles.Jpeg(), fileName, declared);

        // Un JPEG que dice ser PNG contradice lo que declara antes de que importe qué es.
        result.Error.Code.ShouldBe("FILE_TYPE_MISMATCH");
    }

    #endregion

    #region Helpers

    private static byte[] PaddedPng(long length)
    {
        var bytes = new byte[length];
        SampleFiles.Png().CopyTo(bytes, 0);
        return bytes;
    }

    private async Task<Result<FileSignature, BaseError>> InspectSignatureAsync(
        byte[] bytes,
        string fileName,
        string declaredContentType)
    {
        using var content = new MemoryStream(bytes);
        return await _sut.InspectSignatureAsync(content, fileName, declaredContentType, CancellationToken.None);
    }

    private async Task<Result<FileSignature, BaseError>> InspectImageAsync(
        byte[] bytes,
        string fileName,
        string declaredContentType)
    {
        using var content = new MemoryStream(bytes);
        return await _sut.InspectImageAsync(content, fileName, declaredContentType, CancellationToken.None);
    }

    private async Task<Result<FileSignature, BaseError>> InspectAsync(
        byte[] bytes,
        string fileName,
        string declaredContentType)
    {
        using var content = new MemoryStream(bytes);
        return await _sut.InspectAsync(content, fileName, declaredContentType, CancellationToken.None);
    }

    #endregion
}
