using System.Text;
using Sorteo360.Platform.Files;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Files;

public sealed class FilesystemFileStoreTests : IDisposable
{
    /// <summary>SHA-256 de "hola", para comprobar que el resumen es del contenido y no de otra cosa.</summary>
    private const string HolaChecksum = "b221d9dbb083a7f33428d7c2a3c3198ae925614d70210e28716ccaa7cd4ddb79";

    private readonly string _rootPath;
    private readonly FilesystemFileStore _sut;

    public FilesystemFileStoreTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"pgti360-files-{Guid.NewGuid():N}");
        _sut = new FilesystemFileStore(_rootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    #region SaveAsync

    [Fact]
    public async Task SaveAsync_ReturnsTheProviderSizeAndChecksumOfWhatWasWritten_WhenAStreamIsSaved()
    {
        var location = await _sut.SaveAsync(StreamOf("hola"), "foto.jpg", "image/jpeg", CancellationToken.None);

        location.Provider.ShouldBe(FilesystemFileStore.ProviderName);
        location.SizeBytes.ShouldBe(4);
        location.Checksum.ShouldBe(HolaChecksum);
    }

    [Fact]
    public async Task SaveAsync_ReturnsADifferentKey_WhenTheFileNameRepeats()
    {
        var first = await _sut.SaveAsync(StreamOf("uno"), "foto.jpg", "image/jpeg", CancellationToken.None);
        var second = await _sut.SaveAsync(StreamOf("dos"), "foto.jpg", "image/jpeg", CancellationToken.None);

        second.StorageKey.ShouldNotBe(first.StorageKey);
    }

    [Fact]
    public async Task SaveAsync_KeepsTheExtension_WhenItIsHarmless()
    {
        var location = await _sut.SaveAsync(StreamOf("hola"), "Foto Del Salon.JPG", "image/jpeg", CancellationToken.None);

        location.StorageKey.ShouldEndWith(".jpg");
    }

    [Theory]
    [InlineData("sin-extension")]
    [InlineData("raro.js%00")]
    [InlineData("largo.extensiondemasiadolarga")]
    public async Task SaveAsync_DropsTheExtension_WhenItIsNotPlain(string fileName)
    {
        var location = await _sut.SaveAsync(StreamOf("hola"), fileName, "application/octet-stream", CancellationToken.None);

        Path.GetExtension(location.StorageKey).ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WritesTheContentUnderTheRootPath_WhenTheRootDoesNotExistYet()
    {
        var location = await _sut.SaveAsync(StreamOf("hola"), "foto.jpg", "image/jpeg", CancellationToken.None);

        File.Exists(Path.Combine(_rootPath, location.StorageKey)).ShouldBeTrue();
    }

    #endregion

    #region OpenReadAsync

    [Fact]
    public async Task OpenReadAsync_ReturnsWhatWasSaved_WhenTheKeyExists()
    {
        var location = await _sut.SaveAsync(StreamOf("hola"), "foto.jpg", "image/jpeg", CancellationToken.None);

        await using var content = await _sut.OpenReadAsync(location.StorageKey, CancellationToken.None);
        using var reader = new StreamReader(content);

        (await reader.ReadToEndAsync(CancellationToken.None)).ShouldBe("hola");
    }

    [Fact]
    public async Task OpenReadAsync_ThrowsNotFound_WhenTheFileIsGone()
    {
        var location = await _sut.SaveAsync(StreamOf("hola"), "foto.jpg", "image/jpeg", CancellationToken.None);
        File.Delete(Path.Combine(_rootPath, location.StorageKey));

        await Should.ThrowAsync<FileNotFoundInStoreException>(
            () => _sut.OpenReadAsync(location.StorageKey, CancellationToken.None));
    }

    [Fact]
    public async Task OpenReadAsync_ThrowsNotFound_WhenTheKeyWasNeverSaved()
    {
        await Should.ThrowAsync<FileNotFoundInStoreException>(
            () => _sut.OpenReadAsync("2026/08/nunca-existio", CancellationToken.None));
    }

    /// <summary>
    /// Una clave que se escapa de la raíz no es un archivo ausente: es una fila corrupta, y
    /// devolverla como ausente la escondería.
    /// </summary>
    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("2026/../../etc/passwd")]
    public async Task OpenReadAsync_RefusesTheKey_WhenItEscapesTheRoot(string storageKey)
    {
        var exception = await Should.ThrowAsync<FileStoreException>(
            () => _sut.OpenReadAsync(storageKey, CancellationToken.None));

        exception.ShouldNotBeOfType<FileNotFoundInStoreException>();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_RemovesTheContent_WhenTheFileExists()
    {
        var location = await _sut.SaveAsync(StreamOf("hola"), "foto.jpg", "image/jpeg", CancellationToken.None);

        await _sut.DeleteAsync(location.StorageKey, CancellationToken.None);

        File.Exists(Path.Combine(_rootPath, location.StorageKey)).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DoesNothing_WhenTheFileIsAlreadyGone()
    {
        await Should.NotThrowAsync(
            () => _sut.DeleteAsync("2026/08/nunca-existio", CancellationToken.None));
    }

    #endregion

    private static Stream StreamOf(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));
}
