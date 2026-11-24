using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Files;

public sealed class FileServiceTests
{
    private readonly IStoredFileRepository _repository;
    private readonly IFileStore _fileStore;
    private readonly FileService _sut;

    public FileServiceTests()
    {
        _repository = Substitute.For<IStoredFileRepository>();
        _fileStore = Substitute.For<IFileStore>();
        _repository.MarkMissingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());
        _repository.ClearMissingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());
        _sut = new FileService(_repository, _fileStore);
    }

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ReturnsTheFile_WhenItExists()
    {
        var file = StoredFileOf();
        _repository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<StoredFile>, BaseError>(Maybe.From(file)));

        var result = await _sut.GetByIdAsync(file.Id, CancellationToken.None);

        result.Value.Value.ShouldBe(file);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNone_WhenTheIdIsUnknown()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<StoredFile>, BaseError>(Maybe<StoredFile>.None));

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Value.HasNoValue.ShouldBeTrue();
    }

    #endregion

    #region OpenContentAsync

    [Fact]
    public async Task OpenContentAsync_ReturnsTheContentWithItsFile_WhenTheStoreHasIt()
    {
        var file = StoredFileOf();
        GivenTheRepositoryHas(file);
        _fileStore.OpenReadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream([1, 2, 3]));

        var result = await _sut.OpenContentAsync(file.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.File.ShouldBe(file);
        result.Value.Value.Content.Length.ShouldBe(3);
    }

    [Fact]
    public async Task OpenContentAsync_ReturnsNone_WhenTheIdIsUnknown()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<StoredFile>, BaseError>(Maybe<StoredFile>.None));

        var result = await _sut.OpenContentAsync(Guid.NewGuid(), CancellationToken.None);

        result.Value.HasNoValue.ShouldBeTrue();
        await _fileStore.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// El almacenamiento contestó que no está: el archivo se perdió, y eso es permanente.
    /// </summary>
    [Fact]
    public async Task OpenContentAsync_ReturnsFileMissing_WhenTheStoreSaysTheFileIsGone()
    {
        var file = StoredFileOf();
        GivenTheRepositoryHas(file);
        _fileStore.OpenReadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns<Stream>(_ => throw new FileNotFoundInStoreException(file.StorageKey));

        var result = await _sut.OpenContentAsync(file.Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_MISSING");
        result.Error.Kind.ShouldBe(ErrorKind.NotFound);
    }

    /// <summary>
    /// El almacenamiento no contestó: no se sabe nada del archivo, y en particular no se sabe
    /// que falte. Es transitorio y se distingue del caso anterior a propósito.
    /// </summary>
    [Fact]
    public async Task OpenContentAsync_ReturnsFileStoreUnavailable_WhenTheStoreDoesNotAnswer()
    {
        var file = StoredFileOf();
        GivenTheRepositoryHas(file);
        _fileStore.OpenReadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns<Stream>(_ => throw new FileStoreUnavailableException("sin almacenamiento"));

        var result = await _sut.OpenContentAsync(file.Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_STORE_UNAVAILABLE");
        result.Error.Kind.ShouldBe(ErrorKind.Unavailable);
    }

    [Fact]
    public async Task OpenContentAsync_ReturnsFailure_WhenTheRepositoryFails()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Maybe<StoredFile>, BaseError>(new BaseError("Error de base de datos", "DB_ERROR")));

        var result = await _sut.OpenContentAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DB_ERROR");
    }

    /// <summary>
    /// El almacenamiento contestó que no está: además del error, la ausencia queda registrada
    /// en la ficha desde ese momento, que es lo que después permite ir a buscarla.
    /// </summary>
    [Fact]
    public async Task OpenContentAsync_ReturnsFileMissingAfterMarkingTheRecord_WhenTheStoreSaysTheFileIsGone()
    {
        var file = StoredFileOf();
        GivenTheRepositoryHas(file);
        GivenTheStoreHasLostTheBytesOf(file);

        var result = await _sut.OpenContentAsync(file.Id, CancellationToken.None);

        result.Error.Code.ShouldBe("FILE_MISSING");
        await _repository.Received(1).MarkMissingAsync(file.Id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Si el sello no se puede escribir, el archivo sigue sin estar: lo que el cliente recibe no
    /// depende de que la anotación haya salido bien.
    /// </summary>
    [Fact]
    public async Task OpenContentAsync_ReturnsFileMissing_WhenTheMarkCannotBeWritten()
    {
        var file = StoredFileOf();
        GivenTheRepositoryHas(file);
        GivenTheStoreHasLostTheBytesOf(file);
        _repository.MarkMissingAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(UnitResult.Failure(new BaseError("Error de base de datos", "DB_ERROR")));

        var result = await _sut.OpenContentAsync(file.Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("FILE_MISSING");
    }

    /// <summary>
    /// El caso que este diseño existe para evitar: un almacenamiento caído no da por perdido
    /// ningún archivo. Un reinicio de cinco minutos no puede marcar la biblioteca entera.
    /// </summary>
    [Fact]
    public async Task OpenContentAsync_ReturnsFileStoreUnavailableWithoutAMissingMark_WhenTheStoreDoesNotAnswer()
    {
        var file = StoredFileOf();
        GivenTheRepositoryHas(file);
        _fileStore.OpenReadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns<Stream>(_ => throw new FileStoreUnavailableException("sin almacenamiento"));

        var result = await _sut.OpenContentAsync(file.Id, CancellationToken.None);

        result.Error.Code.ShouldBe("FILE_STORE_UNAVAILABLE");
        await _repository.DidNotReceive().MarkMissingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Un timeout es un almacenamiento que no contesta, no un archivo que no está: llega como
    /// la misma avería transitoria y tampoco toca ninguna fila.
    /// </summary>
    [Fact]
    public async Task OpenContentAsync_ReturnsNoMissingMark_WhenTheStoreTimesOut()
    {
        var file = StoredFileOf();
        GivenTheRepositoryHas(file);
        _fileStore.OpenReadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns<Stream>(_ => throw new FileStoreUnavailableException(
                "el almacenamiento no respondió a tiempo", new TimeoutException()));

        var result = await _sut.OpenContentAsync(file.Id, CancellationToken.None);

        result.Error.Code.ShouldBe("FILE_STORE_UNAVAILABLE");
        await _repository.DidNotReceive().MarkMissingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenContentAsync_ReturnsTheFileWithItsMarkUntouched_WhenTheStoreHasTheFile()
    {
        var file = StoredFileOf();
        GivenTheRepositoryHas(file);
        _fileStore.OpenReadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream([1, 2, 3]));

        var result = await _sut.OpenContentAsync(file.Id, CancellationToken.None);

        result.Value.Value.File.MissingSince.ShouldBeNull();
        await _repository.DidNotReceive().MarkMissingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().ClearMissingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Alguien lo restauró desde la papelera: la marca se borra y la ficha que se devuelve ya
    /// no dice que falte, porque en ese mismo momento dejó de faltar.
    /// </summary>
    [Fact]
    public async Task OpenContentAsync_ReturnsAFileNoLongerMarkedMissing_WhenAMissingFileComesBack()
    {
        var file = StoredFileOf() with { MissingSince = DateTimeOffset.UtcNow.AddDays(-2) };
        GivenTheRepositoryHas(file);
        _fileStore.OpenReadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream([1, 2, 3]));

        var result = await _sut.OpenContentAsync(file.Id, CancellationToken.None);

        await _repository.Received(1).ClearMissingAsync(file.Id, Arg.Any<CancellationToken>());
        result.Value.Value.File.MissingSince.ShouldBeNull();
    }

    #endregion

    #region ListMissingAsync

    [Fact]
    public async Task ListMissingAsync_ReturnsThePageTheRepositoryGives_WhenTheRepositorySucceeds()
    {
        var file = StoredFileOf() with { MissingSince = DateTimeOffset.UtcNow };
        var page = new PagedResult<StoredFile>([file], Total: 1, Page: 1, PageSize: 20);
        _repository.ListMissingAsync(1, 20, Arg.Any<CancellationToken>())
            .Returns(Result.Success<PagedResult<StoredFile>, BaseError>(page));

        var result = await _sut.ListMissingAsync(1, 20, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(page);
    }

    [Fact]
    public async Task ListMissingAsync_ReturnsFailure_WhenTheRepositoryFails()
    {
        _repository.ListMissingAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PagedResult<StoredFile>, BaseError>(
                new BaseError("Error de base de datos", "DB_ERROR")));

        var result = await _sut.ListMissingAsync(1, 20, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DB_ERROR");
    }

    #endregion

    private void GivenTheRepositoryHas(StoredFile file)
    {
        _repository.GetByIdAsync(file.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<StoredFile>, BaseError>(Maybe.From(file)));
    }

    private void GivenTheStoreHasLostTheBytesOf(StoredFile file)
    {
        _fileStore.OpenReadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns<Stream>(_ => throw new FileNotFoundInStoreException(file.StorageKey));
    }

    private static StoredFile StoredFileOf()
    {
        return new StoredFile(
            id: Guid.NewGuid(),
            provider: "filesystem",
            storageKey: "2026/08/abcdef.jpg",
            fileName: "salon.jpg",
            contentType: "image/jpeg",
            sizeBytes: 3,
            checksum: "abc123",
            missingSince: null,
            createdAt: DateTimeOffset.UtcNow,
            createdById: Guid.NewGuid());
    }
}
