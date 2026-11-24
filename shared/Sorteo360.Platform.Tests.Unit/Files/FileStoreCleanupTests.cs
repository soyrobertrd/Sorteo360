using Sorteo360.Platform.Files;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Platform.Tests.Unit.Files;

public sealed class FileStoreCleanupTests
{
    private const string StorageKey = "2026/08/abcdef";

    private readonly IFileStore _fileStore = Substitute.For<IFileStore>();

    [Fact]
    public async Task DiscardOrphanBytesAsync_DeletesTheKey_WhenTheStoreIsAvailable()
    {
        await _fileStore.DiscardOrphanBytesAsync(StorageKey);

        await _fileStore.Received(1).DeleteAsync(StorageKey, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Si quien subió el archivo cortó, la limpieza igual tiene que correr: cancelarla dejaría
    /// justamente los bytes que nadie va a referenciar.
    /// </summary>
    [Fact]
    public async Task DiscardOrphanBytesAsync_DeletesWithCancellationTokenNone_WhenCalled()
    {
        await _fileStore.DiscardOrphanBytesAsync(StorageKey);

        await _fileStore.Received(1).DeleteAsync(StorageKey, CancellationToken.None);
    }

    /// <summary>
    /// Que la limpieza falle no cambia lo que se le responde a quien subió el archivo, así que su
    /// avería no se propaga; lo que queda es un huérfano, que la purga de retención barre.
    /// </summary>
    [Fact]
    public async Task DiscardOrphanBytesAsync_DoesNotThrow_WhenTheStoreIsUnavailable()
    {
        _fileStore
            .DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new FileStoreUnavailableException("sin almacenamiento")));

        await Should.NotThrowAsync(() => _fileStore.DiscardOrphanBytesAsync(StorageKey));
    }

    /// <summary>
    /// Solo se traga los fallos del almacenamiento. Un error de programación no es una avería de
    /// infraestructura y no debe quedar escondido acá.
    /// </summary>
    [Fact]
    public async Task DiscardOrphanBytesAsync_Throws_WhenTheFailureIsNotAStoreFailure()
    {
        _fileStore
            .DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("error de programación")));

        await Should.ThrowAsync<InvalidOperationException>(() => _fileStore.DiscardOrphanBytesAsync(StorageKey));
    }

    [Fact]
    public async Task DiscardOrphanBytesAsync_ThrowsArgumentNullException_WhenThereIsNoStore()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            FileStoreCleanup.DiscardOrphanBytesAsync(null!, StorageKey));
    }
}
