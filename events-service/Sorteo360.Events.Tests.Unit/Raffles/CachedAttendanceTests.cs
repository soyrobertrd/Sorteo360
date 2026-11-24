using CSharpFunctionalExtensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Directories;
using Sorteo360.Events.Infrastructure.Raffles;
using Sorteo360.Events.Tests.Unit.Shared;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// La copia de la lista de la puerta que sirven las lecturas del borrador. Lo que se prueba es la
/// escalera de degradación vista desde el sorteo: una caída del seam sirve la copia anterior con su
/// fecha original, y solo sin ninguna copia se dice <c>unavailable</c>.
/// </summary>
public sealed class CachedAttendanceTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly TestTimeProvider _time = new();
    private readonly IAttendanceSource _source = Substitute.For<IAttendanceSource>();
    private readonly IDistributedCache _cache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private CachedAttendance CreateSut() => new(
        _source,
        _cache,
        Options.Create(new RaffleAttendanceSettings { CacheTtlSeconds = (int)Ttl.TotalSeconds }),
        new RaffleAttendanceRefreshLock(),
        _time,
        NullLogger<CachedAttendance>.Instance);

    [Fact]
    public async Task GetAsync_ReturnsTheFreshCopyWithoutCallingTheSeam_WhenItIsWithinTheTtl()
    {
        SourceReturns(Person("ana@agencia.com"));
        var sut = CreateSut();
        var first = await sut.GetAsync(EventId, CancellationToken.None);

        _time.Advance(Ttl - TimeSpan.FromSeconds(1));
        var second = await sut.GetAsync(EventId, CancellationToken.None);

        await _source.Received(1).GetCheckedInAsync(EventId, Arg.Any<CancellationToken>());
        second.Status.ShouldBe(AttendanceStatus.Ok);
        second.FetchedAt.ShouldBe(first.FetchedAt);
        second.Attendees.ShouldNotBeNull().ShouldHaveSingleItem().EmailNormalized.ShouldBe("ana@agencia.com");
    }

    [Fact]
    public async Task GetAsync_ReturnsTheRefreshedList_WhenTheCopyHasExpired()
    {
        SourceReturns(Person("ana@agencia.com"));
        var sut = CreateSut();
        await sut.GetAsync(EventId, CancellationToken.None);

        SourceReturns(Person("ana@agencia.com"), Person("luis@agencia.com"));
        _time.Advance(Ttl);
        var refreshed = await sut.GetAsync(EventId, CancellationToken.None);

        await _source.Received(2).GetCheckedInAsync(EventId, Arg.Any<CancellationToken>());
        refreshed.Status.ShouldBe(AttendanceStatus.Ok);
        refreshed.FetchedAt.ShouldBe(_time.GetUtcNow());
        refreshed.Attendees.ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_ReturnsThePreviousCopyAsStale_WhenTheRefreshFails()
    {
        SourceReturns(Person("ana@agencia.com"));
        var sut = CreateSut();
        var first = await sut.GetAsync(EventId, CancellationToken.None);

        SourceFails();
        _time.Advance(Ttl + TimeSpan.FromMinutes(5));
        var stale = await sut.GetAsync(EventId, CancellationToken.None);

        stale.Status.ShouldBe(AttendanceStatus.Stale);
        stale.FetchedAt.ShouldBe(first.FetchedAt);
        stale.Attendees.ShouldNotBeNull().ShouldHaveSingleItem().EmailNormalized.ShouldBe("ana@agencia.com");
    }

    [Fact]
    public async Task GetAsync_ReturnsUnavailable_WhenThereWasNeverACopy()
    {
        SourceFails();

        var result = await CreateSut().GetAsync(EventId, CancellationToken.None);

        result.Status.ShouldBe(AttendanceStatus.Unavailable);
        result.FetchedAt.ShouldBeNull();
        result.Attendees.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsAnEmptyListAsOk_WhenNobodyHasCheckedInYet()
    {
        // Nadie llegó todavía es una respuesta, no una caída: tiene que guardarse y servirse como
        // tal, o la lectura diría "unavailable" toda la tarde previa a abrir la puerta.
        SourceReturns();

        var result = await CreateSut().GetAsync(EventId, CancellationToken.None);

        result.Status.ShouldBe(AttendanceStatus.Ok);
        result.Attendees.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_KeepsOneCopyPerEvent_WhenTwoEventsAreRead()
    {
        var otherEvent = Guid.NewGuid();
        _source.GetCheckedInAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<Attendee>, BaseError>([Person("ana@agencia.com")]));
        _source.GetCheckedInAsync(otherEvent, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<Attendee>, BaseError>([Person("luis@agencia.com")]));
        var sut = CreateSut();

        var first = await sut.GetAsync(EventId, CancellationToken.None);
        var other = await sut.GetAsync(otherEvent, CancellationToken.None);

        first.Attendees.ShouldNotBeNull().ShouldHaveSingleItem().EmailNormalized.ShouldBe("ana@agencia.com");
        other.Attendees.ShouldNotBeNull().ShouldHaveSingleItem().EmailNormalized.ShouldBe("luis@agencia.com");
    }

    private void SourceReturns(params Attendee[] attendees) =>
        _source.GetCheckedInAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<Attendee>, BaseError>(attendees));

    private void SourceFails() =>
        _source.GetCheckedInAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<Attendee>, BaseError>(RaffleErrors.AttendanceUnavailable()));

    private static Attendee Person(string email) => new(
        email, "Nombre", "Apellido", "Agencia", Guid.NewGuid(), AttendeeOrigin.Invited,
        new DateTimeOffset(2026, 10, 1, 19, 0, 0, TimeSpan.Zero));
}
