using Sorteo360.Platform.Files;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Files;

/// <summary>
/// Lo que decide la clase de un archivo son sus bytes. Acá se comprueba justamente eso: el mismo
/// contenido se reconoce igual venga con el nombre que venga.
/// </summary>
public sealed class FileSignaturesTests
{
    #region Detect

    [Fact]
    public void Detect_ReturnsJpeg_WhenTheBytesAreJpeg()
    {
        FileSignatures.Detect(SampleFiles.Jpeg()).ShouldBe(FileSignatures.Jpeg);
    }

    [Fact]
    public void Detect_ReturnsPng_WhenTheBytesArePng()
    {
        FileSignatures.Detect(SampleFiles.Png()).ShouldBe(FileSignatures.Png);
    }

    [Fact]
    public void Detect_ReturnsWebp_WhenTheBytesAreWebp()
    {
        FileSignatures.Detect(SampleFiles.Webp()).ShouldBe(FileSignatures.Webp);
    }

    [Fact]
    public void Detect_ReturnsPdf_WhenTheBytesArePdf()
    {
        FileSignatures.Detect(SampleFiles.Pdf()).ShouldBe(FileSignatures.Pdf);
    }

    [Fact]
    public void Detect_ReturnsHeic_WhenTheBytesAreHeic()
    {
        FileSignatures.Detect(SampleFiles.Heic()).ShouldBe(FileSignatures.Heic);
    }

    [Fact]
    public void Detect_ReturnsNothing_WhenTheContentIsNoneOfThem()
    {
        FileSignatures.Detect(SampleFiles.Html()).ShouldBeNull();
    }

    [Fact]
    public void Detect_ReturnsNothing_WhenThereAreNoBytes()
    {
        FileSignatures.Detect([]).ShouldBeNull();
    }

    /// <summary>
    /// Un contenedor RIFF también lo usa un WAV, y uno ISO-BMFF también lo usa un MP4: la marca
    /// que viene después es la que decide, y sin ella no hay coincidencia.
    /// </summary>
    [Fact]
    public void Detect_ReturnsNothing_WhenTheContainerHasAnotherBrand()
    {
        byte[] wav = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVEfmt "u8];
        byte[] mp4 = [0x00, 0x00, 0x00, 0x18, .. "ftypisom"u8, 0x00, 0x00, 0x02, 0x00];

        FileSignatures.Detect(wav).ShouldBeNull();
        FileSignatures.Detect(mp4).ShouldBeNull();
    }

    /// <summary>
    /// Un encabezado cortado no puede llevar la marca que falta, así que no coincide en vez de
    /// leer fuera de lo que llegó.
    /// </summary>
    [Fact]
    public void Detect_ReturnsNothing_WhenTheContainerIsTruncated()
    {
        FileSignatures.Detect("RIFF"u8).ShouldBeNull();
    }

    #endregion

    #region The catalogue

    [Fact]
    public void Accepted_ReturnsNoHeic_WhenRead()
    {
        FileSignatures.Accepted.ShouldNotContain(FileSignatures.Heic);
    }

    [Fact]
    public void Accepted_ReturnsExactlyTheAgreedKinds_WhenRead()
    {
        FileSignatures.Accepted.Select(s => s.ContentType)
            .ShouldBe(["image/jpeg", "image/png", "image/webp", "application/pdf"], ignoreOrder: true);
    }

    #endregion

    #region Declarations

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("IMAGE/JPEG")]
    [InlineData("image/jpg")]
    public void AcceptsContentType_ReturnsTrue_WhenTheTypeIsASynonymOfTheSameKind(string declared)
    {
        FileSignatures.Jpeg.AcceptsContentType(declared).ShouldBeTrue();
    }

    [Fact]
    public void AcceptsContentType_ReturnsFalse_WhenTheTypeIsOfAnotherKind()
    {
        FileSignatures.Jpeg.AcceptsContentType("application/pdf").ShouldBeFalse();
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".JPEG")]
    public void AcceptsExtension_ReturnsTrue_WhenTheCaseDiffers(string extension)
    {
        FileSignatures.Jpeg.AcceptsExtension(extension).ShouldBeTrue();
    }

    [Fact]
    public void AcceptsExtension_ReturnsFalse_WhenTheExtensionIsOfAnotherKind()
    {
        FileSignatures.Jpeg.AcceptsExtension(".png").ShouldBeFalse();
    }

    #endregion
}
