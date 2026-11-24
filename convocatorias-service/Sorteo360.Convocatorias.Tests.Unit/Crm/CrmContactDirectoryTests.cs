using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Infrastructure.Crm;
using Sorteo360.Convocatorias.Tests.Unit.Neighbours;
using Sorteo360.Platform.Errors;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Crm;

/// <summary>
/// El paginado de la selección de destinatarios. Lo que se prueba acá no es el mapeo de una ficha
/// sino cuándo el lector decide que la lista <b>no</b> está entera: media selección devuelta como
/// si fuera la entera es la avería invisible que la spec <c>03</c> §6 existe para impedir.
/// </summary>
public sealed class CrmContactDirectoryTests
{
    private const int PageSize = 100;
    private const int MaxPages = 100;

    private readonly ICrmContactsClient _client = Substitute.For<ICrmContactsClient>();
    private readonly RecordingLogger<CrmContactDirectory> _logger = new();
    private readonly CrmContactDirectory _directory;

    public CrmContactDirectoryTests() => _directory = new CrmContactDirectory(_client, _logger);

    #region Cuándo la lista está entera

    [Fact]
    public async Task ListContactsAsync_ReturnsThePagesJoined_WhenAShortPageClosesTheList()
    {
        SetUpPages(Page(PageSize), Page(PageSize), Page(7));

        var result = await ListAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(207);
    }

    [Fact]
    public async Task ListContactsAsync_ReturnsTheWholeListInOneCall_WhenTheFirstPageIsShort()
    {
        SetUpPages(Page(3));

        var result = await ListAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(3);
        await _client.Received(1).ListContactsAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListContactsAsync_ReturnsAnEmptyListNotAFailure_WhenThePageIsEmpty()
    {
        SetUpPages(Page(0));

        var result = await ListAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    #endregion

    #region Cuándo la lista no está entera

    [Fact]
    public async Task ListContactsAsync_ReturnsCrmUnavailableNotATruncatedList_WhenA200HasNoBody()
    {
        SetUpPages(Page(PageSize), null);

        var result = await ListAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListContactsAsync_ReturnsCrmUnavailable_WhenTheCeilingIsHitWithAFullPage()
    {
        var pages = Enumerable.Repeat(Page(PageSize), MaxPages).ToArray();
        SetUpPages(pages);

        var result = await ListAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    #endregion

    #region Averías del vecino, y quien se fue

    [Fact]
    public async Task ListContactsAsync_ReturnsCrmUnavailable_WhenTheCrmDoesNotAnswer()
    {
        _client.ListContactsAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("caído"));

        var result = await ListAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListContactsAsync_ReturnsCrmUnavailable_WhenTheCrmTimesOut()
    {
        _client.ListContactsAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("tardó demasiado"));

        var result = await ListAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListContactsAsync_ThrowsTaskCanceledNotAnOutage_WhenTheCallerWalkedAway()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _client.ListContactsAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("quien preguntaba se fue"));

        await Should.ThrowAsync<TaskCanceledException>(
            () => _directory.ListContactsAsync([], [], cancellation.Token));

        _logger.Errors.ShouldBeEmpty();
    }

    #endregion

    #region La búsqueda por correo

    [Fact]
    public async Task FindByEmailAsync_ReturnsCrmUnavailable_WhenTheCrmDoesNotAnswer()
    {
        _client.FindContactByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("caído"));

        var result = await _directory.FindByEmailAsync("ana@example.com", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task FindByEmailAsync_ReturnsCrmUnavailable_WhenTheCrmTimesOut()
    {
        _client.FindContactByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("tardó demasiado"));

        var result = await _directory.FindByEmailAsync("ana@example.com", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Un 200 sin cuerpo no es "no está": informar la fila como nueva la crearía por duplicado.
    /// </summary>
    [Fact]
    public async Task FindByEmailAsync_ReturnsCrmUnavailable_WhenA200HasNoBody()
    {
        _client.FindContactByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CrmContactPageResponse?)null);

        var result = await _directory.FindByEmailAsync("ana@example.com", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
    }

    [Fact]
    public async Task FindByEmailAsync_ReturnsNone_WhenTheCrmHasNoSuchContact()
    {
        _client.FindContactByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CrmContactPageResponse([], TotalCount: 0, Page: 1, PageSize: PageSize));

        var result = await _directory.FindByEmailAsync("ana@example.com", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public async Task FindByEmailAsync_Throws_WhenTheCallerWalkedAway()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _client.FindContactByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("quien preguntaba se fue"));

        await Should.ThrowAsync<TaskCanceledException>(
            () => _directory.FindByEmailAsync("ana@example.com", cancellation.Token));

        _logger.Errors.ShouldBeEmpty();
    }

    #endregion

    #region El alta de un contacto

    [Fact]
    public async Task CreateContactAsync_ReturnsCrmUnavailable_WhenTheCrmDoesNotAnswer()
    {
        _client.CreateContactAsync(Arg.Any<CrmCreateContactRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("caído"));

        var result = await _directory.CreateContactAsync(NewContact(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task CreateContactAsync_ReturnsCrmUnavailable_WhenTheCrmTimesOut()
    {
        _client.CreateContactAsync(Arg.Any<CrmCreateContactRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("tardó demasiado"));

        var result = await _directory.CreateContactAsync(NewContact(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Un 409 es una coincidencia y no un error: es lo que hace reanudable una confirmación que
    /// se cortó después de que el alta ya había entrado.
    /// </summary>
    [Fact]
    public async Task CreateContactAsync_ReturnsTheExistingContact_WhenTheCrmAnswers409()
    {
        var existing = new CrmContactResponse(Guid.NewGuid(), "Ana", "Pérez", "ana@example.com", false, "opted_out");

        _client.CreateContactAsync(Arg.Any<CrmCreateContactRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CrmCreateContactResponse(CreatedId: null, ExistingContact: existing));

        var result = await _directory.CreateContactAsync(NewContact(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AlreadyExisted.ShouldBeTrue();
        result.Value.Contact.Id.ShouldBe(existing.Id);
        result.Value.Contact.CanBeEmailed.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateContactAsync_ReturnsCrmUnavailable_WhenAnAcceptedCreateCarriesNoId()
    {
        _client.CreateContactAsync(Arg.Any<CrmCreateContactRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CrmCreateContactResponse(CreatedId: Guid.Empty, ExistingContact: null));

        var result = await _directory.CreateContactAsync(NewContact(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
    }

    [Fact]
    public async Task CreateContactAsync_Throws_WhenTheCallerWalkedAway()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _client.CreateContactAsync(Arg.Any<CrmCreateContactRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("quien preguntaba se fue"));

        await Should.ThrowAsync<TaskCanceledException>(
            () => _directory.CreateContactAsync(NewContact(), cancellation.Token));

        _logger.Errors.ShouldBeEmpty();
    }

    #endregion

    #region La selección por ids

    [Fact]
    public async Task ListContactsByIdAsync_ReturnsTheBatchesJoined_WhenTheIdsSpanSeveralCalls()
    {
        var ids = Enumerable.Range(0, 250).Select(_ => Guid.NewGuid()).ToList();
        var batches = new List<IReadOnlyList<Guid>>();
        _client.ListContactsByIdAsync(
                Arg.Do<IReadOnlyList<Guid>>(batch => batches.Add([.. batch])),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(call => PageOf(call.Arg<IReadOnlyList<Guid>>().Take(2)));

        var result = await _directory.ListContactsByIdAsync([.. ids, ids[0]], CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(6);
        batches.Select(batch => batch.Count).ShouldBe([100, 100, 50]);
        batches.SelectMany(batch => batch).ShouldBe(ids);
    }

    [Fact]
    public async Task ListContactsByIdAsync_ReturnsCrmUnavailable_WhenALaterBatchFails()
    {
        var ids = Enumerable.Range(0, 150).Select(_ => Guid.NewGuid()).ToList();
        _client.ListContactsByIdAsync(
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(PageOf(ids.Take(1)), (CrmContactPageResponse?)null);

        var result = await _directory.ListContactsByIdAsync(ids, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<CrmUnavailableError>();
    }

    [Fact]
    public async Task ListContactsByIdAsync_ReturnsAnEmptyListWithoutCallingTheCrm_WhenNoIdIsGiven()
    {
        var result = await _directory.ListContactsByIdAsync([], CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        await _client.DidNotReceiveWithAnyArgs().ListContactsByIdAsync(default!, default, default, default);
    }

    #endregion

    #region La lista de contactos y su OPT (PGTI-173)

    [Fact]
    public async Task FindContactListAsync_ReturnsTheListWithItsOptAndMembers_WhenTheCrmHasIt()
    {
        var (listId, optOfficeId, member) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _client.GetContactListAsync(listId, Arg.Any<CancellationToken>())
            .Returns(new CrmByIdRead<CrmContactListResponse>(true, new(listId, optOfficeId, [member])));

        var result = await _directory.FindContactListAsync(listId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.Id.ShouldBe(listId);
        result.Value.Value.OptOfficeId.ShouldBe(optOfficeId);
        result.Value.Value.ContactIds.ShouldBe([member]);
    }

    [Fact]
    public async Task FindContactListAsync_ReturnsNone_WhenTheCrmAnswers404()
    {
        _client.GetContactListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CrmByIdRead<CrmContactListResponse>(false, null));

        var result = await _directory.FindContactListAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasNoValue.ShouldBeTrue();
    }

    /// <summary>Un 200 sin cuerpo no es "no está": es un CRM que contestó sin decir nada.</summary>
    [Fact]
    public async Task FindContactListAsync_ReturnsCrmUnavailable_WhenA200HasNoBody()
    {
        _client.GetContactListAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CrmByIdRead<CrmContactListResponse>(true, null));

        var result = await _directory.FindContactListAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CrmUnavailableError.ErrorCode);
    }

    #endregion

    private static CrmContactPageResponse PageOf(IEnumerable<Guid> ids)
    {
        var items = ids
            .Select(id => new CrmContactResponse(id, "Ana", "Pérez", "ana@acme.com", true, null))
            .ToList();

        return new(items, TotalCount: items.Count, Page: 1, PageSize: PageSize);
    }

    private static NewCrmContact NewContact() =>
        new("Ana", "Pérez", "ana@example.com", null, ConsentSources.VerbalUpload);

    private Task<Result<IReadOnlyList<CrmContact>, BaseError>> ListAsync() =>
        _directory.ListContactsAsync([], [], CancellationToken.None);

    private void SetUpPages(params CrmContactPageResponse?[] pages)
    {
        var call = _client.ListContactsAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());

        call.Returns(pages[0], pages.Skip(1).ToArray());
    }

    private static CrmContactPageResponse Page(int items) =>
        new(
            Enumerable.Range(0, items)
                .Select(_ => new CrmContactResponse(Guid.NewGuid(), "Ana", "Pérez", "ana@acme.com", true, null))
                .ToList(),
            TotalCount: items,
            Page: 1,
            PageSize: PageSize);
}
