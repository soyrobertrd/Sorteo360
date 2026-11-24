using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Infrastructure.Events;
using Sorteo360.Convocatorias.Tests.Unit.Neighbours;
using Sorteo360.Platform.Errors;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Events;

/// <summary>
/// El paginado de la cartelera pública. Es la misma pregunta que en el CRM —cuándo el lector
/// decide que la lista no está entera— y se prueba aparte a propósito: esta ruta está en internet,
/// así que una truncación silenciosa acá la ve un visitante y no un operador.
/// </summary>
public sealed class EventDirectoryTests
{
    private const int PageSize = 100;
    private const int MaxPages = 100;

    private readonly IEventsClient _client = Substitute.For<IEventsClient>();
    private readonly RecordingLogger<EventDirectory> _logger = new();
    private readonly EventDirectory _directory;

    public EventDirectoryTests() => _directory = new EventDirectory(_client, _logger);

    #region Cuándo la cartelera está entera

    [Fact]
    public async Task ListPublicEventsAsync_ReturnsThePagesJoined_WhenAShortPageClosesTheList()
    {
        SetUpPages(Page(PageSize), Page(PageSize), Page(7));

        var result = await ListAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(207);
    }

    [Fact]
    public async Task ListPublicEventsAsync_ReturnsAnEmptyListingNotAFailure_WhenThePageIsEmpty()
    {
        SetUpPages(Page(0));

        var result = await ListAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListPublicEventsAsync_ReturnsOnlyPublicUnfinishedEvents_WhenThePageMixesInPrivateOrFinished()
    {
        SetUpPages(new EventPageResponse(
            [
                Item(EventVisibility.Public, EventStatus.Planning),
                Item(EventVisibility.Private, EventStatus.Planning),
                Item(EventVisibility.Public, EventStatus.Cancelled)
            ],
            Total: 3,
            Page: 1,
            PageSize: PageSize));

        var result = await ListAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
    }

    #endregion

    #region Cuándo la cartelera no está entera

    [Fact]
    public async Task ListPublicEventsAsync_ReturnsEventsUnavailableNotATruncatedList_WhenA200HasNoBody()
    {
        SetUpPages(Page(PageSize), null);

        var result = await ListAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(EventsUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListPublicEventsAsync_ReturnsEventsUnavailable_WhenTheCeilingIsHitWithAFullPage()
    {
        SetUpPages(Enumerable.Repeat(Page(PageSize), MaxPages).ToArray());

        var result = await ListAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(EventsUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    #endregion

    #region Averías del vecino, y quien se fue

    [Fact]
    public async Task ListPublicEventsAsync_ReturnsEventsUnavailable_WhenEventsServiceDoesNotAnswer()
    {
        ListCall().ThrowsAsync(new HttpRequestException("caído"));

        var result = await ListAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(EventsUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListPublicEventsAsync_ReturnsEventsUnavailable_WhenEventsServiceTimesOut()
    {
        ListCall().ThrowsAsync(new TaskCanceledException("tardó demasiado"));

        var result = await ListAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(EventsUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListPublicEventsAsync_ThrowsTaskCanceledNotAnOutage_WhenTheVisitorWalkedAway()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        ListCall().ThrowsAsync(new TaskCanceledException("el visitante cerró la pestaña"));

        await Should.ThrowAsync<TaskCanceledException>(
            () => _directory.ListPublicEventsAsync(cancellation.Token));

        _logger.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEventAsync_ThrowsTaskCanceledNotAnOutage_WhenTheVisitorWalkedAway()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _client.GetEventAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("el visitante cerró la pestaña"));

        await Should.ThrowAsync<TaskCanceledException>(
            () => _directory.GetEventAsync(Guid.NewGuid(), cancellation.Token));

        _logger.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEventAsync_ReturnsEventsUnavailable_WhenEventsServiceDoesNotAnswer()
    {
        _client.GetEventAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("caído"));

        var result = await _directory.GetEventAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(EventsUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetEventAsync_ReturnsEventsUnavailable_WhenEventsServiceTimesOut()
    {
        _client.GetEventAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("tardó demasiado"));

        var result = await _directory.GetEventAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(EventsUnavailableError.ErrorCode);
        _logger.Errors.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Nulo es un 404: un id que events-service no conoce es un éxito vacío, no una avería.
    /// </summary>
    [Fact]
    public async Task GetEventAsync_ReturnsNone_WhenEventsServiceDoesNotKnowTheId()
    {
        _client.GetEventAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((EventItemResponse?)null);

        var result = await _directory.GetEventAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasNoValue.ShouldBeTrue();
        _logger.Errors.ShouldBeEmpty();
    }

    #endregion

    private Task<Result<IReadOnlyList<EventSummary>, BaseError>> ListAsync() =>
        _directory.ListPublicEventsAsync(CancellationToken.None);

    private Task<EventPageResponse?> ListCall() =>
        _client.ListActiveEventsAsync(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());

    private void SetUpPages(params EventPageResponse?[] pages) =>
        ListCall().Returns(pages[0], pages.Skip(1).ToArray());

    private static EventPageResponse Page(int items) =>
        new(
            Enumerable.Range(0, items)
                .Select(_ => Item(EventVisibility.Public, EventStatus.Planning))
                .ToList(),
            Total: items,
            Page: 1,
            PageSize: PageSize);

    private static EventItemResponse Item(string visibility, string status) =>
        new(
            Guid.NewGuid(),
            "Feria de Punta Cana",
            "fair",
            status,
            "es",
            visibility,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 3),
            null,
            null,
            null,
            null);
}
