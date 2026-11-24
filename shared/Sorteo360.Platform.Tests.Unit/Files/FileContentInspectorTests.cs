using CSharpFunctionalExtensions;
using Sorteo360.Platform.Files;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Files;

/// <summary>
/// La inspección entera: qué entra, qué no y por qué motivo. Lo que declara el cliente aparece
/// en casi todos los casos porque es justamente lo que no decide. Qué código y qué mensaje recibe
/// quien lo intentó lo decide cada servicio, y se prueba allá.
/// </summary>
public sealed class FileContentInspectorTests
{
    #region What is accepted

    [Fact]
    public async Task InspectAsync_ReturnsTheJpegKind_WhenTheBytesAreJpeg()
    {
        var result = await InspectAsync(SampleFiles.Jpeg(), "salon.jpg", "image/jpeg");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileSignatures.Jpeg);
    }

    [Theory]
    [InlineData("salon.png", "image/png", "image/png")]
    [InlineData("salon.webp", "image/webp", "image/webp")]
    [InlineData("cotizacion.pdf", "application/pdf", "application/pdf")]
    public async Task InspectAsync_ReturnsTheKind_WhenTheFormatIsAccepted(string fileName, string declared, string expected)
    {
        var bytes = expected switch
        {
            "image/png" => SampleFiles.Png(),
            "image/webp" => SampleFiles.Webp(),
            _ => SampleFiles.Pdf()
        };

        var result = await InspectAsync(bytes, fileName, declared);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe(expected);
    }

    /// <summary>Quien no declara nada no contradice nada: el contenido decide solo.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsTheKind_WhenTheCallerDeclaresNothing()
    {
        var result = await InspectAsync(SampleFiles.Pdf(), "cotizacion", FileSignatures.UndeclaredContentType);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileSignatures.Pdf);
    }

    [Fact]
    public async Task InspectAsync_ReturnsTheKind_WhenTheDeclaredTypeIsMissing()
    {
        var result = await InspectAsync(SampleFiles.Pdf(), "cotizacion.pdf", null);

        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>El tipo puede llegar con parámetros; lo que se compara es el tipo.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsTheKind_WhenTheDeclaredTypeCarriesParameters()
    {
        var result = await InspectAsync(SampleFiles.Jpeg(), "salon.jpg", "image/jpeg; charset=binary");

        result.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// Los bytes se leen para reconocerlos y se dejan como estaban: quien llama entrega el mismo
    /// stream al almacenamiento justo después.
    /// </summary>
    [Fact]
    public async Task InspectAsync_ReturnsTheStreamAtItsStartingPosition_WhenTheHeaderWasRead()
    {
        using var content = new MemoryStream([0x00, 0x00, .. SampleFiles.Jpeg()]);
        content.Position = 2;

        var result = await FileContentInspector.InspectAsync(
            content, "salon.jpg", "image/jpeg", FileContentInspector.DefaultMaxSizeBytes, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        content.Position.ShouldBe(2);
    }

    /// <summary>El límite lo pone quien llama: el mismo archivo pasa con el cupo puesto más alto.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsTheKind_WhenTheLimitIsRaised()
    {
        using var content = new MemoryStream([.. SampleFiles.Jpeg(), .. new byte[64]]);

        var result = await FileContentInspector.InspectAsync(content, "salon.jpg", "image/jpeg", 1024, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region A caller's own list

    /// <summary>Quien llama puede sumar una clase a la lista: un libro de Excel entra con su tipo.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsTheXlsxKind_WhenTheCallerAcceptsWorkbooks()
    {
        var result = await InspectAsync(SampleFiles.Xlsx(), "tarifas.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", WithWorkbooks);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(FileSignatures.Xlsx);
        result.Value.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    /// <summary>
    /// Lo que se parece a un libro y no lo es: el mismo contenedor ZIP sin libro (un .docx, un ZIP
    /// renombrado), un libro con macros, o el .xls viejo, cuyo contenedor es el de un .doc.
    /// </summary>
    [Theory]
    [InlineData("xlsm", "tarifas.xlsm")]
    [InlineData("xlsm", "tarifas.xlsx")]
    [InlineData("docx", "contrato.docx")]
    [InlineData("zip", "tarifas.zip")]
    [InlineData("zip", "tarifas.xlsx")]
    [InlineData("xls", "tarifas.xls")]
    public async Task InspectAsync_ReturnsTypeNotAllowed_WhenTheContentOnlyLooksLikeAWorkbook(string sample, string fileName)
    {
        var bytes = sample switch
        {
            "xlsm" => SampleFiles.Xlsm(),
            "docx" => SampleFiles.Docx(),
            "zip" => SampleFiles.Zip(),
            _ => SampleFiles.Xls()
        };

        var result = await InspectAsync(bytes, fileName, FileSignatures.UndeclaredContentType, WithWorkbooks);

        result.Error.ShouldBe(FileRejection.TypeNotAllowed);
    }

    /// <summary>
    /// El stream de un formulario de ASP.NET no se queja como un <see cref="MemoryStream"/> al
    /// buscar antes del principio. Algo que no es un ZIP entero, o que solo empieza como uno, se
    /// rechaza igual en vez de romper la subida.
    /// </summary>
    [Theory]
    [InlineData("xls")]
    [InlineData("truncated-zip")]
    public async Task InspectAsync_ReturnsTypeNotAllowed_WhenAShortFileArrivesOnAFormStream(string sample)
    {
        byte[] bytes = sample == "xls" ? SampleFiles.Xls() : [.. SampleFiles.Xlsx().Take(12)];
        using var content = new FormLikeStream(bytes);

        var result = await FileContentInspector.InspectAsync(content, "tarifas.xlsx", null,
            FileContentInspector.DefaultMaxSizeBytes, WithWorkbooks, CancellationToken.None);

        result.Error.ShouldBe(FileRejection.TypeNotAllowed);
    }

    /// <summary>Sin que quien llama lo pida, un libro de Excel sigue sin entrar.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsTypeNotAllowed_WhenAWorkbookArrivesWithThePlatformList()
    {
        var result = await InspectAsync(SampleFiles.Xlsx(), "tarifas.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        result.Error.ShouldBe(FileRejection.TypeNotAllowed);
    }

    /// <summary>Un libro que dice ser un PDF se rechaza como cualquier otro disfraz.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsTypeMismatch_WhenAWorkbookClaimsToBeAPdf()
    {
        var result = await InspectAsync(SampleFiles.Xlsx(), "tarifas.pdf", "application/pdf", WithWorkbooks);

        result.Error.ShouldBe(FileRejection.TypeMismatch);
    }

    /// <summary>Abrir el paquete no mueve el stream: el almacenamiento lo recibe desde donde llegó.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsTheStreamAtItsStartingPosition_WhenAWorkbookWasOpened()
    {
        using var content = new MemoryStream(SampleFiles.Xlsx());

        var result = await FileContentInspector.InspectAsync(content, "tarifas.xlsx", null,
            FileContentInspector.DefaultMaxSizeBytes, WithWorkbooks, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        content.Position.ShouldBe(0);
    }

    #endregion

    #region What is refused

    [Fact]
    public async Task InspectAsync_ReturnsTooLarge_WhenTheFileExceedsTheLimit()
    {
        using var content = new MemoryStream([.. SampleFiles.Jpeg(), .. new byte[64]]);

        var result = await FileContentInspector.InspectAsync(content, "salon.jpg", "image/jpeg", 32, CancellationToken.None);

        result.Error.ShouldBe(FileRejection.TooLarge);
    }

    [Fact]
    public async Task InspectAsync_ReturnsTypeNotAllowed_WhenTheContentIsOfNoKnownKind()
    {
        var result = await InspectAsync(SampleFiles.Html(), "salon.jpg", "image/jpeg");

        result.Error.ShouldBe(FileRejection.TypeNotAllowed);
    }

    [Fact]
    public async Task InspectAsync_ReturnsTypeNotAllowed_WhenThereAreNoBytes()
    {
        var result = await InspectAsync([], "salon.jpg", "image/jpeg");

        result.Error.ShouldBe(FileRejection.TypeNotAllowed);
    }

    /// <summary>Un PDF disfrazado de imagen se rechaza en vez de corregirse.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsTypeMismatch_WhenTheContentContradictsTheDeclaredType()
    {
        var result = await InspectAsync(SampleFiles.Pdf(), "salon.png", "image/png");

        result.Error.ShouldBe(FileRejection.TypeMismatch);
    }

    [Fact]
    public async Task InspectAsync_ReturnsTypeMismatch_WhenTheExtensionContradictsTheContent()
    {
        var result = await InspectAsync(SampleFiles.Pdf(), "cotizacion.jpg", "application/pdf");

        result.Error.ShouldBe(FileRejection.TypeMismatch);
    }

    [Fact]
    public async Task InspectAsync_ReturnsHeicNotSupported_WhenTheContentIsHeic()
    {
        var result = await InspectAsync(SampleFiles.Heic(), "IMG_0001.heic", "image/heic");

        result.Error.ShouldBe(FileRejection.HeicNotSupported);
    }

    /// <summary>Un HEIC renombrado a .jpg sigue siendo un HEIC, y se rechaza por lo mismo.</summary>
    [Fact]
    public async Task InspectAsync_ReturnsHeicNotSupported_WhenAHeicClaimsToBeAJpeg()
    {
        var result = await InspectAsync(SampleFiles.Heic(), "IMG_0001.jpg", "image/jpeg");

        result.Error.ShouldBe(FileRejection.HeicNotSupported);
    }

    #endregion

    #region Guards

    [Fact]
    public async Task InspectAsync_Throws_WhenTheContentCannotBeRewound()
    {
        using var content = new ForwardOnlyStream(SampleFiles.Jpeg());

        await Should.ThrowAsync<ArgumentException>(() => FileContentInspector.InspectAsync(
            content, "salon.jpg", "image/jpeg", FileContentInspector.DefaultMaxSizeBytes, CancellationToken.None));
    }

    [Fact]
    public void DefaultMaxSizeBytes_ReturnsTwentyFiveMegabytes_WhenRead()
    {
        FileContentInspector.DefaultMaxSizeBytes.ShouldBe(25L * 1024 * 1024);
    }

    #endregion

    #region Helpers

    /// <summary>La lista que pide el CRM para los archivos de una empresa.</summary>
    private static readonly IReadOnlyList<FileSignature> WithWorkbooks = [.. FileSignatures.Accepted, FileSignatures.Xlsx];

    private static async Task<Result<FileSignature, FileRejection>> InspectAsync(
        byte[] bytes,
        string fileName,
        string? declaredContentType)
    {
        using var content = new MemoryStream(bytes);
        return await FileContentInspector.InspectAsync(
            content, fileName, declaredContentType, FileContentInspector.DefaultMaxSizeBytes, CancellationToken.None);
    }

    private static async Task<Result<FileSignature, FileRejection>> InspectAsync(
        byte[] bytes,
        string fileName,
        string? declaredContentType,
        IReadOnlyList<FileSignature> accepted)
    {
        using var content = new MemoryStream(bytes);
        return await FileContentInspector.InspectAsync(
            content, fileName, declaredContentType, FileContentInspector.DefaultMaxSizeBytes, accepted, CancellationToken.None);
    }

    /// <summary>
    /// Como el stream de un archivo de formulario en ASP.NET: buscar fuera del contenido lanza
    /// <see cref="ArgumentOutOfRangeException"/>, no la <see cref="IOException"/> de un <see cref="MemoryStream"/>.
    /// </summary>
    private sealed class FormLikeStream : MemoryStream
    {
        public FormLikeStream(byte[] bytes) : base(bytes)
        {
        }

        public override long Seek(long offset, SeekOrigin loc)
        {
            var target = loc switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                _ => Length + offset
            };

            ArgumentOutOfRangeException.ThrowIfNegative(target, nameof(offset));
            return base.Seek(offset, loc);
        }
    }

    /// <summary>Un stream que no admite búsqueda, como el de una tubería.</summary>
    private sealed class ForwardOnlyStream : MemoryStream
    {
        public ForwardOnlyStream(byte[] bytes) : base(bytes)
        {
        }

        public override bool CanSeek => false;
    }

    #endregion
}
