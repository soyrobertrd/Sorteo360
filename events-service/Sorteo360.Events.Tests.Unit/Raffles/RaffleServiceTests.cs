using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.CrmDirectory;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class RaffleServiceTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IRaffleRepository _repository;
    private readonly IEventRepository _eventRepository;
    private readonly IRecentAttendance _attendance;
    private readonly IAttendanceSource _liveAttendance;
    private readonly IAwardsMembership _awards;
    private readonly IContactMailability _mailability;
    private readonly CrmDirectoryStatusContext _crmStatus = new();
    private readonly IFileStore _fileStore = Substitute.For<IFileStore>();
    private readonly INotarialRecordRenderer _renderer = Substitute.For<INotarialRecordRenderer>();
    private readonly IRaffleOperatorDirectory _operators = Substitute.For<IRaffleOperatorDirectory>();
    private readonly IRaffleWinnerNotifier _winnerNotifier = Substitute.For<IRaffleWinnerNotifier>();
    private readonly RaffleService _sut;

    public RaffleServiceTests()
    {
        _repository = Substitute.For<IRaffleRepository>();
        _eventRepository = Substitute.For<IEventRepository>();
        _attendance = Substitute.For<IRecentAttendance>();
        _liveAttendance = Substitute.For<IAttendanceSource>();
        _awards = Substitute.For<IAwardsMembership>();
        _mailability = Substitute.For<IContactMailability>();
        _sut = new RaffleService(
            _repository, _eventRepository, _attendance, _liveAttendance, _awards, _mailability, _crmStatus,
            Substitute.For<IUploadPolicy>(), _fileStore, _renderer, _operators, _winnerNotifier,
            Substitute.For<ICompanyDirectory>(), Substitute.For<ICompanyContactDirectory>(), TimeProvider.System);

        // Por defecto el evento existe, admite escrituras y todavía no tiene sorteo; cada test
        // sobreescribe lo que le toca.
        SetupEventStatus(Maybe.From(EventStatus.Planning));
        SetupRaffle(Maybe<Raffle>.None);
        SetupRaffleWithRounds(Maybe<RaffleWithRounds>.None);
        SetupAttendance(new RecentAttendance(AttendanceStatus.Unavailable, null, null));
        SetupAwards(Maybe<AwardsList>.None);
        SetupOverrides();
        SetupLiveDoor();
        _repository.UpsertOverrideAsync(Arg.Any<Guid>(), Arg.Any<AttendeeOverride>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());
        _repository.DeleteOverrideAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());

        // El CRM contesta por todos y no veta a nadie, salvo que el test diga otra cosa.
        _mailability.CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result.Success<IReadOnlyDictionary<Guid, string?>, BaseError>(
                callInfo.Arg<IReadOnlyCollection<Guid>>().ToDictionary(id => id, _ => (string?)null)));
        _repository.AddAsync(Arg.Any<Raffle>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result.Success<Guid, BaseError>(callInfo.Arg<Raffle>().Id));
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ReturnsTheNewRaffleId_WhenTheEventHasNone()
    {
        var result = await _sut.CreateAsync(EventId, UserId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).AddAsync(
            Arg.Is<Raffle>(r => r.Id == result.Value && r.EventId == EventId && r.State == RaffleState.Draft),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ReturnsEventNotFound_WhenTheEventDoesNotExist()
    {
        SetupEventStatus(Maybe<string>.None);

        var result = await _sut.CreateAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_FOUND");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Raffle>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ReturnsRaffleAlreadyExists_WhenTheEventHasOne()
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));

        var result = await _sut.CreateAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_ALREADY_EXISTS");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Raffle>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task CreateAsync_ReturnsEventNotEditable_WhenTheEventIsOver(string status)
    {
        SetupEventStatus(Maybe.From(status));

        var result = await _sut.CreateAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Raffle>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ReturnsRaffleAlreadyExists_WhenTheEventIsClosedAndAlreadyHasOne()
    {
        // El duplicado es el error más específico, así que gana sobre el estado del evento.
        SetupEventStatus(Maybe.From(EventStatus.Closed));
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));

        var result = await _sut.CreateAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_ALREADY_EXISTS");
    }

    #endregion

    #region GetAsync

    [Fact]
    public async Task GetAsync_ReturnsTheRaffleWithItsRounds_WhenTheEventHasOne()
    {
        var raffle = WithRounds(RaffleWithRoundsTests.Round(1));
        SetupRaffleWithRounds(Maybe.From(raffle));

        var result = await _sut.GetAsync(EventId, CancellationToken.None);

        result.Value.Raffle.ShouldBe(raffle);
    }

    [Fact]
    public async Task GetAsync_ReturnsTheRaffle_WhenTheEventIsClosed()
    {
        // Leer nunca consulta si el evento admite escrituras.
        SetupEventStatus(Maybe.From(EventStatus.Closed));
        SetupRaffleWithRounds(Maybe.From(WithRounds()));

        var result = await _sut.GetAsync(EventId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_ReturnsRaffleNotFound_WhenTheEventHasNone()
    {
        var result = await _sut.GetAsync(EventId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task GetAsync_ReturnsEventNotFound_WhenTheEventDoesNotExist()
    {
        SetupEventStatus(Maybe<string>.None);

        var result = await _sut.GetAsync(EventId, CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_FOUND");
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ReturnsSuccessPersistingTheReplacedFields_WhenDraft()
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));
        _repository.UpdateAsync(Arg.Any<Raffle>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());

        var result = await _sut.UpdateAsync(UpdateDto(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).UpdateAsync(
            Arg.Is<Raffle>(r => r.AnimationSeconds == 20 && r.VisualStyle == RaffleVisualStyle.SequentialNames),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ReturnsEventNotFound_WhenTheEventDoesNotExist()
    {
        SetupEventStatus(Maybe<string>.None);

        var result = await _sut.UpdateAsync(UpdateDto(), CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsRaffleNotFound_WhenTheEventHasNone()
    {
        var result = await _sut.UpdateAsync(UpdateDto(), CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_FOUND");
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task UpdateAsync_ReturnsEventNotEditable_WhenTheEventIsOver(string status)
    {
        SetupEventStatus(Maybe.From(status));
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));

        var result = await _sut.UpdateAsync(UpdateDto(), CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Raffle>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ReturnsRaffleCompleted_WhenCompleted()
    {
        var completed = Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow) with { State = RaffleState.Completed };
        SetupRaffle(Maybe.From(completed));

        var result = await _sut.UpdateAsync(UpdateDto(), CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_COMPLETED");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Raffle>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Rounds and prizes

    public static TheoryData<string> SetupWrites =>
    [
        nameof(IRaffleService.CreateRoundAsync),
        nameof(IRaffleService.UpdateRoundAsync),
        nameof(IRaffleService.DeleteRoundAsync),
        nameof(IRaffleService.CreatePrizesAsync),
        nameof(IRaffleService.UpdatePrizeAsync),
        nameof(IRaffleService.DeletePrizeAsync),
    ];

    [Theory]
    [MemberData(nameof(SetupWrites))]
    public async Task SetupWrite_ReturnsEventNotFound_WhenTheEventDoesNotExist(string write)
    {
        SetupEventStatus(Maybe<string>.None);

        var error = await InvokeAsync(write);

        error.ShouldNotBeNull().Code.ShouldBe("EVENT_NOT_FOUND");
    }

    [Theory]
    [MemberData(nameof(SetupWrites))]
    public async Task SetupWrite_ReturnsRaffleNotFound_WhenTheEventHasNone(string write)
    {
        var error = await InvokeAsync(write);

        error.ShouldNotBeNull().Code.ShouldBe("RAFFLE_NOT_FOUND");
    }

    [Theory]
    [MemberData(nameof(SetupWrites))]
    public async Task SetupWrite_ReturnsEventNotEditable_WhenTheEventIsClosedAndTheRaffleCompleted(string write)
    {
        // El evento gana sobre el sorteo: es el primero en el orden de comprobaciones.
        SetupEventStatus(Maybe.From(EventStatus.Closed));
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Completed)));

        var error = await InvokeAsync(write);

        error.ShouldNotBeNull().Code.ShouldBe("EVENT_NOT_EDITABLE");
    }

    [Theory]
    [MemberData(nameof(SetupWrites))]
    public async Task SetupWrite_ReturnsRaffleCompleted_WhenCompleted(string write)
    {
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Completed)));

        var error = await InvokeAsync(write);

        error.ShouldNotBeNull().Code.ShouldBe("RAFFLE_COMPLETED");
        _repository.ReceivedCalls()
            .Select(c => c.GetMethodInfo().Name)
            .ShouldBe([nameof(IRaffleRepository.GetWithRoundsByEventIdAsync)]);
    }

    [Theory]
    [InlineData(nameof(IRaffleService.UpdateRoundAsync), "ROUND_NOT_FOUND")]
    [InlineData(nameof(IRaffleService.DeleteRoundAsync), "ROUND_NOT_FOUND")]
    [InlineData(nameof(IRaffleService.CreatePrizesAsync), "ROUND_NOT_FOUND")]
    [InlineData(nameof(IRaffleService.UpdatePrizeAsync), "PRIZE_NOT_FOUND")]
    [InlineData(nameof(IRaffleService.DeletePrizeAsync), "PRIZE_NOT_FOUND")]
    public async Task SetupWrite_ReturnsNotFound_WhenTheChildIsNotOfThisRaffle(string write, string code)
    {
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Draft, RaffleWithRoundsTests.Round(1))));

        var error = await InvokeAsync(write);

        error.ShouldNotBeNull().Code.ShouldBe(code);
    }

    [Fact]
    public async Task CreateRoundAsync_ReturnsTheNewId_WhenInProgress()
    {
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.InProgress, RaffleWithRoundsTests.Round(1))));
        _repository.AddRoundAsync(Arg.Any<RaffleRound>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result.Success<Guid, BaseError>(callInfo.Arg<RaffleRound>().Id));

        var result = await _sut.CreateRoundAsync(
            new CreateRaffleRoundDto(EventId, "Ronda final", null, UserId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).AddRoundAsync(
            Arg.Is<RaffleRound>(r => r.Id == result.Value && r.Number == 2 && r.Name == "Ronda final"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteRoundAsync_ReturnsRoundNotEmpty_WhenTheRoundHoldsAPrize()
    {
        var round = RaffleWithRoundsTests.Round(1);
        var full = round with { Prizes = [RaffleWithRoundsTests.PrizeIn(round.Id, DateTimeOffset.UtcNow)] };
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Draft, full)));

        var result = await _sut.DeleteRoundAsync(EventId, round.Id, CancellationToken.None);

        result.Error.Code.ShouldBe("ROUND_NOT_EMPTY");
        await _repository.DidNotReceive().DeleteRoundAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteRoundAsync_ReturnsRoundNotFound_WhenTheRowWentAwayBeforeTheWrite()
    {
        var round = RaffleWithRoundsTests.Round(1);
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Draft, round)));
        _repository.DeleteRoundAsync(Arg.Any<Guid>(), round.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<bool, BaseError>(false));

        var result = await _sut.DeleteRoundAsync(EventId, round.Id, CancellationToken.None);

        result.Error.Code.ShouldBe("ROUND_NOT_FOUND");
    }

    [Fact]
    public async Task CreatePrizesAsync_ReturnsQuantityIdenticalPrizes_WhenQuantityIsFive()
    {
        var round = RaffleWithRoundsTests.Round(1);
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Draft, round)));
        _repository.AddPrizesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Prize>>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());

        var result = await _sut.CreatePrizesAsync(
            new CreatePrizesDto(EventId, round.Id, "Bolsa", "De tela", "RD$500", null, null, 5, UserId),
            CancellationToken.None);

        var prizes = result.Value;
        prizes.Count.ShouldBe(5);
        prizes.Select(p => p.Id).Distinct().Count().ShouldBe(5);
        prizes.ShouldAllBe(p => p.Name == "Bolsa" && p.RoundId == round.Id && p.Value == "RD$500");
        prizes.Select(p => p.CreatedAt).Distinct().Count().ShouldBe(1);
        (prizes[0].CreatedAt.Ticks % TimeSpan.TicksPerMicrosecond).ShouldBe(0);
        await _repository.Received(1).AddPrizesAsync(
            Arg.Any<Guid>(), Arg.Is<IReadOnlyList<Prize>>(l => l.Count == 5), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePrizesAsync_PersistsFreeTextSponsorNameWithoutCallingCrm_WhenSponsorCompanyIdIsNull()
    {
        var round = RaffleWithRoundsTests.Round(1);
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Draft, round)));
        _repository.AddPrizesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Prize>>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());

        var result = await _sut.CreatePrizesAsync(
            new CreatePrizesDto(EventId, round.Id, "Tablet", null, "RD$15,000", null, null, 2, UserId, "Banco Popular"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldAllBe(p => p.SponsorName == "Banco Popular" && p.SponsorCompanyId == null);
        await _repository.Received(1).AddPrizesAsync(
            Arg.Any<Guid>(),
            Arg.Is<IReadOnlyList<Prize>>(l => l.Count == 2 && l.All(p => p.SponsorName == "Banco Popular")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePrizeAsync_ReturnsSuccessMovingThePrize_WhenTheTargetRoundIsOfThisRaffle()
    {
        var source = RaffleWithRoundsTests.Round(1);
        var target = RaffleWithRoundsTests.Round(2);
        var prize = RaffleWithRoundsTests.PrizeIn(source.Id, DateTimeOffset.UtcNow);
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Draft, source with { Prizes = [prize] }, target)));
        _repository.UpdatePrizeAsync(
                Arg.Any<Guid>(), Arg.Any<Prize>(), Arg.Any<Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<bool, BaseError>(true));

        var result = await _sut.UpdatePrizeAsync(
            new UpdatePrizeDto(EventId, prize.Id, target.Id, "Ron", null, null, null, null, UserId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).UpdatePrizeAsync(
            Arg.Any<Guid>(),
            Arg.Is<Prize>(p => p.Id == prize.Id && p.RoundId == target.Id && p.Name == "Ron"),
            Arg.Any<Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePrizeAsync_ReturnsRoundNotFound_WhenTheTargetRoundIsNotOfThisRaffle()
    {
        var round = RaffleWithRoundsTests.Round(1);
        var prize = RaffleWithRoundsTests.PrizeIn(round.Id, DateTimeOffset.UtcNow);
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Draft, round with { Prizes = [prize] })));

        var result = await _sut.UpdatePrizeAsync(
            new UpdatePrizeDto(EventId, prize.Id, Guid.NewGuid(), "Ron", null, null, null, null, UserId), CancellationToken.None);

        result.Error.Code.ShouldBe("ROUND_NOT_FOUND");
        await _repository.DidNotReceive().UpdatePrizeAsync(
            Arg.Any<Guid>(), Arg.Any<Prize>(), Arg.Any<Func<IReadOnlyList<PrizeDraw>, UnitResult<BaseError>>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Invoca una de las seis escrituras del armado con identificadores que no están en ningún
    /// sorteo, y devuelve su error, o null si tuvo éxito.
    /// </summary>
    private async Task<BaseError?> InvokeAsync(string write)
    {
        var ct = CancellationToken.None;
        var unknown = Guid.NewGuid();

        return write switch
        {
            nameof(IRaffleService.CreateRoundAsync) => ErrorOf(await _sut.CreateRoundAsync(
                new CreateRaffleRoundDto(EventId, "Ronda", null, UserId), ct)),
            nameof(IRaffleService.UpdateRoundAsync) => ErrorOf(await _sut.UpdateRoundAsync(
                new UpdateRaffleRoundDto(EventId, unknown, "Ronda", null, UserId), ct)),
            nameof(IRaffleService.DeleteRoundAsync) => ErrorOf(await _sut.DeleteRoundAsync(EventId, unknown, ct)),
            nameof(IRaffleService.CreatePrizesAsync) => ErrorOf(await _sut.CreatePrizesAsync(
                new CreatePrizesDto(EventId, unknown, "Bolsa", null, null, null, null, 1, UserId), ct)),
            nameof(IRaffleService.UpdatePrizeAsync) => ErrorOf(await _sut.UpdatePrizeAsync(
                new UpdatePrizeDto(EventId, unknown, unknown, "Bolsa", null, null, null, null, UserId), ct)),
            nameof(IRaffleService.DeletePrizeAsync) => ErrorOf(await _sut.DeletePrizeAsync(EventId, unknown, ct)),
            _ => throw new ArgumentOutOfRangeException(nameof(write), write, null),
        };
    }

    private static BaseError? ErrorOf<T>(Result<T, BaseError> result) => result.IsFailure ? result.Error : null;

    private static BaseError? ErrorOf(UnitResult<BaseError> result) => result.IsFailure ? result.Error : null;

    #endregion

    #region GetAsync — readiness

    [Fact]
    public async Task GetAsync_ReturnsOnlyAttendanceUnavailable_WhenThereIsNoCopyOfTheDoorList()
    {
        // Sin lista no se sabe si el bombo está vacío: decir no_qualified_attendees sería inventarlo.
        SetupRaffleWithRounds(Maybe.From(WithPrize()));

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.IsReadyToStart.ShouldBe(false);
        readiness.BlockingReasons.ShouldBe([RaffleBlockingReason.AttendanceUnavailable]);
    }

    [Fact]
    public async Task GetAsync_ReturnsNoQualifiedAttendees_WhenTheDrumIsEmpty()
    {
        SetupRaffleWithRounds(Maybe.From(WithPrize()));
        var pedro = Person("pedro@agencia.com");
        SetupDoor(pedro);
        SetupOverrides(new AttendeeOverride("pedro@agencia.com", AttendeeOverrideDecision.Excluded, "Excluido", UserId, DateTimeOffset.UtcNow));

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.BlockingReasons.ShouldBe([RaffleBlockingReason.NoQualifiedAttendees]);
    }

    [Fact]
    public async Task GetAsync_ReturnsReadyToStart_WhenSomebodyQualifies()
    {
        SetupRaffleWithRounds(Maybe.From(WithPrize()));
        SetupDoor(Person("ana@agencia.com"));

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.IsReadyToStart.ShouldBe(true);
        readiness.BlockingReasons.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_ReturnsAttendanceTooManyRowsAndSucceeds_WhenTheDoorIsOverTheCeiling()
    {
        SetupRaffleWithRounds(Maybe.From(WithPrize()));
        SetupDoor(Door(25_001));

        var result = await _sut.GetAsync(EventId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Readiness.BlockingReasons.ShouldBe([RaffleBlockingReason.AttendanceTooManyRows]);
    }

    [Fact]
    public async Task GetAsync_ReturnsTheWarningsInForce_WhenTheCopyIsStaleWithNoListAndNoPrizes()
    {
        SetupRaffleWithRounds(Maybe.From(WithRounds()));
        SetupAttendance(new RecentAttendance(AttendanceStatus.Stale, DateTimeOffset.UtcNow, [Person("ana@agencia.com")]));

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.Warnings.ShouldBe(
            [RaffleWarning.AwardsListNotInForce, RaffleWarning.AttendanceCopyStale, RaffleWarning.NoPrizes]);
        readiness.IsReadyToStart.ShouldBe(true);
    }

    [Fact]
    public async Task GetAsync_ReturnsNoAwardsWarning_WhenAListIsInForce()
    {
        SetupRaffleWithRounds(Maybe.From(WithPrize()));
        SetupDoor(Person("ana@agencia.com"));
        SetupAwards(Maybe.From(new AwardsList(Guid.NewGuid(), new HashSet<string> { "ana@agencia.com" })));

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.Warnings.ShouldBeEmpty();
        readiness.IsReadyToStart.ShouldBe(true);
    }

    [Theory]
    [InlineData(RaffleState.InProgress)]
    [InlineData(RaffleState.Completed)]
    public async Task GetAsync_ReturnsNoReadinessAndReadsNoAttendance_WhenTheRaffleIsNotADraft(string state)
    {
        SetupRaffleWithRounds(Maybe.From(WithRounds(state)));

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.IsReadyToStart.ShouldBeNull();
        readiness.BlockingReasons.ShouldBeNull();
        readiness.Warnings.ShouldBe([RaffleWarning.AwardsListNotInForce, RaffleWarning.NoPrizes]);
        await _attendance.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _awards.DidNotReceive().GetInForceAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(RaffleState.InProgress)]
    [InlineData(RaffleState.Completed)]
    public async Task GetAsync_ReturnsNoAwardsWarning_WhenTheRaffleStartedWithAListInForce(string state)
    {
        var raffle = WithPrize();
        SetupRaffleWithRounds(Maybe.From(raffle with
        {
            Raffle = raffle.Raffle with { State = state, AwardsBatchId = Guid.NewGuid() },
        }));
        // Una lista en vigor ahora no cambia nada: lo que cuenta es la que se aplicó al arrancar.
        SetupAwards(Maybe<AwardsList>.None);

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.Warnings.ShouldBeEmpty();
    }

    #endregion

    #region GetAttendeesAsync

    [Fact]
    public async Task GetAttendeesAsync_ReturnsRaffleNotFound_WhenTheEventHasNone()
    {
        var result = await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task GetAttendeesAsync_ReturnsNoClassification_WhenThereIsNoCopyOfTheDoorList()
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));

        var result = await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        result.Value.AttendanceStatus.ShouldBe(AttendanceStatus.Unavailable);
        result.Value.AttendanceFetchedAt.ShouldBeNull();
        result.Value.Attendees.ShouldBeNull();
    }

    [Fact]
    public async Task GetAttendeesAsync_ReturnsAttendanceTooManyRows_WhenTheDoorIsOverTheCeiling()
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));
        SetupDoor(Door(25_001));

        var result = await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_TOO_MANY_ROWS");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public async Task GetAttendeesAsync_ReturnsTheClassification_WhenTheDoorIsExactlyAtTheCeiling()
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));
        SetupDoor(Door(25_000));

        var result = await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        result.Value.Attendees.ShouldNotBeNull().Count.ShouldBe(25_000);
    }

    [Fact]
    public async Task GetAttendeesAsync_ReturnsTheListInForceAndAppliesIt_WhenThereIsOne()
    {
        var batchId = Guid.NewGuid();
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));
        SetupDoor(Person("ana@agencia.com"), Person("luis@agencia.com"));
        SetupAwards(Maybe.From(new AwardsList(batchId, new HashSet<string> { "ana@agencia.com" })));

        var result = await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        result.Value.AwardsBatchId.ShouldBe(batchId);
        result.Value.Attendees.ShouldNotBeNull().Count(a => a.Qualified).ShouldBe(1);
    }

    [Fact]
    public async Task GetAttendeesAsync_ReturnsAttendeesByLastNameThenFirstNameThenEmail_WhenTheDoorIsUnordered()
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));
        SetupDoor(
            Person("b@agencia.com", "Ana", "pérez"),
            Person("z@agencia.com", "luis", "Gómez"),
            Person("a@agencia.com", "Ana", "Pérez"),
            Person("c@agencia.com", "Beatriz", "Gómez"));

        var result = await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        result.Value.Attendees.ShouldNotBeNull().Select(a => a.EmailNormalized)
            .ShouldBe(["c@agencia.com", "z@agencia.com", "a@agencia.com", "b@agencia.com"]);
    }

    [Fact]
    public async Task GetAttendeesAsync_ReturnsAttendeesInSpanishOrder_WhenLastNamesCarryAccentsOrEnye()
    {
        // Una tilde no manda a nadie al final de la lista, y la ñ va después de la n, como en un
        // directorio en español.
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));
        SetupDoor(
            Person("zapata@agencia.com", "Ana", "Zapata"),
            Person("perez@agencia.com", "Ana", "Pérez"),
            Person("nunez2@agencia.com", "Ana", "Ñúñez"),
            Person("alvarez@agencia.com", "Ana", "Álvarez"),
            Person("nunez1@agencia.com", "Ana", "Núñez"));

        var result = await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        result.Value.Attendees.ShouldNotBeNull().Select(a => a.LastName)
            .ShouldBe(["Álvarez", "Núñez", "Ñúñez", "Pérez", "Zapata"]);
    }

    #endregion

    #region La lista negra y las decisiones manuales en la lectura

    [Fact]
    public async Task GetAsync_ReturnsCrmUnavailableAndReportsIt_WhenTheCrmDoesNotAnswer()
    {
        SetupRaffleWithRounds(Maybe.From(WithPrize()));
        SetupDoor(Person("ana@agencia.com"));
        SetupCrmDown();

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.IsReadyToStart.ShouldBe(false);
        readiness.BlockingReasons.ShouldBe([RaffleBlockingReason.CrmUnavailable]);
        _crmStatus.Status.ShouldBe(CrmDirectoryStatus.Unavailable);
    }

    [Fact]
    public async Task GetAsync_ReportsCrmOk_WhenTheCrmAnswers()
    {
        SetupRaffleWithRounds(Maybe.From(WithPrize()));
        SetupDoor(Person("ana@agencia.com"));

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.IsReadyToStart.ShouldBe(true);
        _crmStatus.Status.ShouldBe(CrmDirectoryStatus.Ok);
    }

    [Fact]
    public async Task GetAsync_AsksAndReportsNothing_WhenThereIsNoCopyOfTheDoorList()
    {
        SetupRaffleWithRounds(Maybe.From(WithPrize()));

        await _sut.GetAsync(EventId, CancellationToken.None);

        _crmStatus.Status.ShouldBeNull();
        await _mailability.DidNotReceive().CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_AsksAndReportsNothing_WhenNobodyAtTheDoorHasAContact()
    {
        SetupRaffleWithRounds(Maybe.From(WithPrize()));
        SetupDoor(Person("luis@agencia.com") with { ContactId = null });

        var readiness = (await _sut.GetAsync(EventId, CancellationToken.None)).Value.Readiness;

        readiness.IsReadyToStart.ShouldBe(true);
        readiness.BlockingReasons.ShouldNotBeNull().ShouldBeEmpty();
        _crmStatus.Status.ShouldBeNull();
        await _mailability.DidNotReceive().CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(RaffleState.InProgress)]
    [InlineData(RaffleState.Completed)]
    public async Task GetAsync_AsksTheCrmNothing_WhenTheRaffleIsNotADraft(string state)
    {
        SetupRaffleWithRounds(Maybe.From(WithRounds(state)));

        await _sut.GetAsync(EventId, CancellationToken.None);

        _crmStatus.Status.ShouldBeNull();
        await _mailability.DidNotReceive().CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAttendeesAsync_AppliesTheOverridesAndTheBlacklist_WhenBothAreInPlay()
    {
        var raffle = Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow);
        SetupRaffle(Maybe.From(raffle));
        var vetado = Person("vetado@agencia.com", lastName: "B");
        SetupDoor(Person("ana@sorteo360.local", lastName: "A"), vetado);
        SetupAwards(Maybe.From(new AwardsList(Guid.NewGuid(), new HashSet<string>(["otra@agencia.com"], StringComparer.Ordinal))));
        SetupMailability(id => id == vetado.ContactId ? "do_not_invite" : null);
        SetupOverrides(
            new AttendeeOverride("ana@sorteo360.local", AttendeeOverrideDecision.Included, "Invitada especial", UserId, DateTimeOffset.UtcNow),
            new AttendeeOverride("vetado@agencia.com", AttendeeOverrideDecision.Included, null, UserId, DateTimeOffset.UtcNow));

        var result = await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        var attendees = result.Value.Attendees.ShouldNotBeNull();
        attendees.Select(a => (a.ExclusionReason, a.OverruledReason)).ShouldBe(
        [
            ((string?)null, (string?)ExclusionReason.NotAwardsMember),
            (ExclusionReason.DoNotInvite, ExclusionReason.DoNotInvite),
        ]);
        attendees[0].ManualNote.ShouldBe("Invitada especial");
        await _repository.Received(1).ListOverridesAsync(raffle.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAttendeesAsync_AsksTheCrmOncePerDistinctContact_WhenTwoRowsShareOne()
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow)));
        var ana = Person("ana@agencia.com");
        SetupDoor(ana, Person("ana.otra@agencia.com") with { ContactId = ana.ContactId }, Person("luis@agencia.com") with { ContactId = null });

        await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        await _mailability.Received(1).CheckAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { ana.ContactId!.Value })),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region StartAsync

    [Fact]
    public async Task StartAsync_FreezesTheLiveDoorAndReturnsTheStartedRaffle_WhenSomebodyQualifies()
    {
        var raffle = WithPrize();
        SetupRaffle(Maybe.From(raffle.Raffle));
        SetupLiveDoor(Person("ana@agencia.com"), Person("luis@sorteo360.local"));
        var frozen = SetupStart(raffle.Raffle);
        var started = raffle with { Raffle = raffle.Raffle with { State = RaffleState.InProgress } };
        SetupRaffleWithRounds(Maybe.From(started));

        var result = await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        result.Value.Raffle.ShouldBe(started);
        result.Value.Readiness.IsReadyToStart.ShouldBeNull();
        var freeze = frozen().ShouldNotBeNull();
        freeze.Started.StartedById.ShouldBe(UserId);
        freeze.Attendees.Select(a => (a.EmailNormalized, a.Qualified)).ShouldBe(
            [("ana@agencia.com", true), ("luis@sorteo360.local", true)], ignoreOrder: true);
        await _attendance.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_StampsTheListInForce_WhenThereIsOne()
    {
        var batchId = Guid.NewGuid();
        SetupDraftRaffle(out var raffle);
        SetupLiveDoor(Person("ana@agencia.com"));
        SetupAwards(Maybe.From(new AwardsList(batchId, new HashSet<string> { "ana@agencia.com" })));
        var frozen = SetupStart(raffle);

        await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        frozen().ShouldNotBeNull().Started.AwardsBatchId.ShouldBe(batchId);
    }

    [Fact]
    public async Task StartAsync_AppliesTheOverridesTheRepositoryReadUnderTheLock_WhenThereAreSome()
    {
        SetupDraftRaffle(out var raffle);
        SetupLiveDoor(Person("ana@agencia.com"), Person("luis@agencia.com"));
        var frozen = SetupStart(raffle,
            new AttendeeOverride("luis@agencia.com", AttendeeOverrideDecision.Excluded, "No vino", UserId, DateTimeOffset.UtcNow));

        await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        frozen().ShouldNotBeNull().Attendees.Single(a => a.EmailNormalized == "luis@agencia.com")
            .ExclusionReason.ShouldBe(ExclusionReason.ManuallyExcluded);
        await _repository.DidNotReceive().ListOverridesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_ReturnsRaffleNoParticipants_WhenTheDrumIsEmpty()
    {
        SetupDraftRaffle(out var raffle);
        var ana = Person("ana@sorteo360.local");
        SetupLiveDoor(ana);
        SetupMailability(id => id == ana.ContactId ? "do_not_invite" : null);
        var frozen = SetupStart(raffle);

        var result = await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NO_PARTICIPANTS");
        frozen().ShouldBeNull();
    }

    [Fact]
    public async Task StartAsync_ReturnsEventNotFound_WhenTheEventDoesNotExist()
    {
        SetupEventStatus(Maybe<string>.None);

        var result = await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task StartAsync_ReturnsRaffleNotFound_WhenTheEventHasNone()
    {
        var result = await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task StartAsync_ReturnsEventNotEditableAndReadsNothing_WhenTheEventIsClosed()
    {
        SetupEventStatus(Maybe.From(EventStatus.Closed));
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow) with { State = RaffleState.InProgress }));

        var result = await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
        await _liveAttendance.DidNotReceive().GetCheckedInAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_ReturnsRaffleNotDraftAndReadsNothing_WhenTheRaffleAlreadyStarted()
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow) with { State = RaffleState.InProgress }));

        var result = await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_DRAFT");
        await _liveAttendance.DidNotReceive().GetCheckedInAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_ReturnsAttendanceUnavailableAndWritesNothing_WhenTheDoorDoesNotAnswer()
    {
        SetupDraftRaffle(out _);
        _liveAttendance.GetCheckedInAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<Attendee>, BaseError>(RaffleErrors.AttendanceUnavailable()));

        var result = await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_UNAVAILABLE");
        await AssertNothingStartedAsync();
    }

    [Fact]
    public async Task StartAsync_ReturnsAttendanceTooManyRowsAndAsksTheCrmNothing_WhenTheDoorIsOverTheCeiling()
    {
        SetupDraftRaffle(out _);
        SetupLiveDoor(Door(25_001));

        var result = await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_TOO_MANY_ROWS");
        await _mailability.DidNotReceive().CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await AssertNothingStartedAsync();
    }

    [Fact]
    public async Task StartAsync_ReturnsCrmUnavailableAndWritesNothing_WhenTheCrmDoesNotAnswer()
    {
        // La lectura del borrador degrada; el arranque no: un bombo sin la lista negra no tiene arreglo.
        SetupDraftRaffle(out _);
        SetupLiveDoor(Person("ana@agencia.com"));
        SetupCrmDown();

        var result = await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("CRM_UNAVAILABLE");
        await AssertNothingStartedAsync();
    }

    [Fact]
    public async Task StartAsync_AsksTheCrmOncePerDistinctContact_WhenTwoRowsShareOne()
    {
        SetupDraftRaffle(out var raffle);
        var ana = Person("ana@agencia.com");
        SetupLiveDoor(ana, Person("ana.otra@agencia.com") with { ContactId = ana.ContactId }, Person("luis@agencia.com") with { ContactId = null });
        SetupStart(raffle);

        await _sut.StartAsync(EventId, UserId, CancellationToken.None);

        await _mailability.Received(1).CheckAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { ana.ContactId!.Value })),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetAttendeesAsync — después del arranque

    [Fact]
    public async Task GetAttendeesAsync_ReturnsTheFrozenRowsSortedAndCallsNoSeam_WhenTheRaffleStarted()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var batchId = Guid.NewGuid();
        var raffle = Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow).Start(batchId, UserId, startedAt).Value;
        SetupRaffle(Maybe.From(raffle));
        _repository.ListFrozenAttendeesAsync(raffle.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<RaffleAttendee>, BaseError>(
            [
                Frozen("z@agencia.com", "Ana", "Zapata"),
                Frozen("a@agencia.com", "Ana", "Álvarez"),
            ]));

        var result = await _sut.GetAttendeesAsync(EventId, CancellationToken.None);

        result.Value.AttendanceStatus.ShouldBe(AttendanceStatus.Ok);
        result.Value.AttendanceFetchedAt.ShouldBe(startedAt);
        result.Value.AwardsBatchId.ShouldBe(batchId);
        result.Value.Attendees.ShouldNotBeNull().Select(a => a.LastName).ShouldBe(["Álvarez", "Zapata"]);
        await _attendance.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _liveAttendance.DidNotReceive().GetCheckedInAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _awards.DidNotReceive().GetInForceAsync(Arg.Any<CancellationToken>());
        await _mailability.DidNotReceive().CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        _crmStatus.Status.ShouldBeNull();
    }

    #endregion

    #region SetOverrideAsync

    [Fact]
    public async Task SetOverrideAsync_WritesTheNormalisedEmailAndTheDecider_WhenAQualifierIsIncluded()
    {
        SetupDraftRaffle(out var raffle);
        SetupLiveDoor(Person("ana@agencia.com"));

        var result = await _sut.SetOverrideAsync(
            OverrideDto("  Ana@Agencia.COM ", AttendeeOverrideDecision.Included, "Nota"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).UpsertOverrideAsync(
            raffle.Id,
            Arg.Is<AttendeeOverride>(o =>
                o.EmailNormalized == "ana@agencia.com"
                && o.Decision == AttendeeOverrideDecision.Included
                && o.Note == "Nota"
                && o.DecidedById == UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsEventNotFound_WhenTheEventDoesNotExist()
    {
        SetupEventStatus(Maybe<string>.None);

        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsRaffleNotFound_WhenTheEventHasNone()
    {
        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsEventNotEditable_WhenTheEventIsClosedAndTheRaffleStarted()
    {
        // El estado del evento gana sobre el del sorteo: es el error más general de los dos.
        SetupEventStatus(Maybe.From(EventStatus.Closed));
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow) with { State = RaffleState.InProgress }));

        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
    }

    [Theory]
    [InlineData(RaffleState.InProgress, "RAFFLE_NOT_DRAFT")]
    [InlineData(RaffleState.Completed, "RAFFLE_COMPLETED")]
    public async Task SetOverrideAsync_ReturnsTheStateError_WhenTheRaffleLeftDraft(string state, string code)
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow) with { State = state }));

        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.Error.Code.ShouldBe(code);
        await _liveAttendance.DidNotReceive().GetCheckedInAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsAttendanceUnavailable_WhenTheLiveDoorFails()
    {
        SetupDraftRaffle(out _);
        _liveAttendance.GetCheckedInAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<Attendee>, BaseError>(RaffleErrors.AttendanceUnavailable()));

        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_UNAVAILABLE");
        await _attendance.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsAttendanceTooManyRows_WhenTheLiveDoorIsOverTheCeiling()
    {
        SetupDraftRaffle(out _);
        SetupLiveDoor(Door(25_001));

        var result = await _sut.SetOverrideAsync(OverrideDto("persona0@agencia.com"), CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_TOO_MANY_ROWS");
    }

    [Theory]
    [InlineData(AttendeeOverrideDecision.Included)]
    [InlineData(AttendeeOverrideDecision.Excluded)]
    public async Task SetOverrideAsync_ReturnsParticipantNotCheckedIn_WhenTheEmailIsNotAtTheDoor(string decision)
    {
        SetupDraftRaffle(out _);
        SetupLiveDoor(Person("ana@agencia.com"));

        var result = await _sut.SetOverrideAsync(OverrideDto("luis@agencia.com", decision), CancellationToken.None);

        result.Error.Code.ShouldBe("PARTICIPANT_NOT_CHECKED_IN");
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsSuccess_WhenAWalkInIsIncluded()
    {
        SetupDraftRaffle(out _);
        SetupLiveDoor(Person("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn, ContactId = null });

        var result = await _sut.SetOverrideAsync(OverrideDto("pedro@agencia.com"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _mailability.DidNotReceive().CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsSuccess_WhenSomebodyWithNoContactIsIncluded()
    {
        SetupDraftRaffle(out _);
        SetupLiveDoor(Person("luis@agencia.com") with { ContactId = null });

        var result = await _sut.SetOverrideAsync(OverrideDto("luis@agencia.com"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _mailability.DidNotReceive().CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsSuccess_WhenTheCrmDoesNotReturnTheContact()
    {
        SetupDraftRaffle(out _);
        SetupLiveDoor(Person("ana@agencia.com"));
        _mailability.CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyDictionary<Guid, string?>, BaseError>(new Dictionary<Guid, string?>()));

        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsParticipantDoNotInviteAndWritesNothing_WhenABlacklistedPersonIsIncluded()
    {
        SetupDraftRaffle(out _);
        var ana = Person("ana@agencia.com");
        SetupLiveDoor(ana);
        SetupMailability(_ => "do_not_invite");

        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.Error.Code.ShouldBe("PARTICIPANT_DO_NOT_INVITE");
        await _mailability.Received(1).CheckAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { ana.ContactId!.Value })),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpsertOverrideAsync(Arg.Any<Guid>(), Arg.Any<AttendeeOverride>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsParticipantSuspendedAndWritesNothing_WhenASuspendedPersonIsIncluded()
    {
        SetupDraftRaffle(out _);
        SetupLiveDoor(Person("ana@agencia.com"));
        SetupMailability(_ => "suspended");

        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.Error.Code.ShouldBe("PARTICIPANT_SUSPENDED");
        await _repository.DidNotReceive().UpsertOverrideAsync(Arg.Any<Guid>(), Arg.Any<AttendeeOverride>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsSuccess_WhenTheCrmSaysTheIncludedPersonMayBeInvited()
    {
        // Lo que el CRM diga del correo —opted_out, no_email— ya no llega: null es canBeInvited.
        SetupDraftRaffle(out _);
        SetupLiveDoor(Person("ana@agencia.com"));
        SetupMailability(_ => null);

        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsCrmUnavailableAndWritesNothing_WhenTheCrmIsDownOnAnInclude()
    {
        SetupDraftRaffle(out _);
        SetupLiveDoor(Person("ana@agencia.com"));
        SetupCrmDown();

        var result = await _sut.SetOverrideAsync(OverrideDto("ana@agencia.com"), CancellationToken.None);

        result.Error.Code.ShouldBe("CRM_UNAVAILABLE");
        result.Error.Kind.ShouldBe(ErrorKind.Unavailable);
        await _repository.DidNotReceive().UpsertOverrideAsync(Arg.Any<Guid>(), Arg.Any<AttendeeOverride>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_ReturnsSuccess_WhenAWalkInWithoutContactIsExcludedWithTheCrmDown()
    {
        SetupDraftRaffle(out _);
        SetupLiveDoor(Person("pedro@agencia.com") with { Origin = AttendeeOrigin.WalkIn, ContactId = null });
        SetupCrmDown();

        var result = await _sut.SetOverrideAsync(
            OverrideDto("pedro@agencia.com", AttendeeOverrideDecision.Excluded), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _mailability.DidNotReceive().CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region RemoveOverrideAsync

    [Fact]
    public async Task RemoveOverrideAsync_DeletesTheNormalisedEmailWithoutReadingTheDoor_WhenDraft()
    {
        SetupDraftRaffle(out var raffle);

        var result = await _sut.RemoveOverrideAsync(EventId, " Ana@Agencia.com", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).DeleteOverrideAsync(raffle.Id, "ana@agencia.com", Arg.Any<CancellationToken>());
        await _liveAttendance.DidNotReceive().GetCheckedInAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveOverrideAsync_ReturnsRaffleNotDraft_WhenTheRaffleStarted()
    {
        SetupRaffle(Maybe.From(Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow) with { State = RaffleState.InProgress }));

        var result = await _sut.RemoveOverrideAsync(EventId, "ana@agencia.com", CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_DRAFT");
        await _repository.DidNotReceive().DeleteOverrideAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task RemoveOverrideAsync_ReturnsEventNotEditable_WhenTheEventIsOver(string status)
    {
        SetupEventStatus(Maybe.From(status));
        SetupDraftRaffle(out _);

        var result = await _sut.RemoveOverrideAsync(EventId, "ana@agencia.com", CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
    }

    #endregion

    #region CompleteAsync

    [Fact]
    public async Task CompleteAsync_ReturnsEventNotFound_WhenTheEventDoesNotExist()
    {
        SetupEventStatus(Maybe<string>.None);

        var result = await _sut.CompleteAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_FOUND");
        await _repository.DidNotReceiveWithAnyArgs().CompleteAsync(default, default, default, default!, default);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsRaffleNotFound_WhenTheEventHasNoRaffle()
    {
        var result = await _sut.CompleteAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_FOUND");
        await _repository.DidNotReceiveWithAnyArgs().CompleteAsync(default, default, default, default!, default);
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task CompleteAsync_ReachesTheRepository_WhenTheEventIsNoLongerEditable(string status)
    {
        // Un sorteo que arrancó tiene que poder terminar, sea cual sea el estado del evento (spec 00 §5).
        SetupEventStatus(Maybe.From(status));
        var raffle = SetupRaffleToComplete();
        _repository.CompleteAsync(default, default, default, default!, default)
            .ReturnsForAnyArgs(UnitResult.Failure<BaseError>(RaffleErrors.Completed()));

        var result = await _sut.CompleteAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_COMPLETED");
        await _repository.Received(1).CompleteAsync(
            raffle.Id, UserId, Arg.Any<DateTimeOffset>(), Arg.Any<Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_SavesThePdfUnderTheRecordNumberWithTheResolvedNames_WhenTheRepositoryProducesIt()
    {
        var raffle = SetupRaffleToComplete();
        var facts = FactsFor(raffle);
        var names = new Dictionary<Guid, string?> { [UserId] = "Ana Pérez" };
        _operators.ResolveNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(names);
        _renderer.Render(Arg.Any<NotarialRecordContent>()).Returns([0x25, 0x50, 0x44, 0x46]);
        _fileStore.SaveAsync(default!, default!, default!, default).ReturnsForAnyArgs(Location("acta-key"));
        StoredFile? produced = null;
        _repository.CompleteAsync(default, default, default, default!, default).ReturnsForAnyArgs(async call =>
        {
            var produce = call.ArgAt<Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>>>(3);
            produced = (await produce(facts, 1, null)).Value;
            return UnitResult.Failure<BaseError>(RaffleErrors.Completed());
        });

        await _sut.CompleteAsync(EventId, UserId, CancellationToken.None);

        var number = NotarialRecordContent.NumberOf(raffle.Id);
        await _operators.Received(1).ResolveNamesAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(facts.OperatorIds())), Arg.Any<CancellationToken>());
        _renderer.Received(1).Render(Arg.Is<NotarialRecordContent>(c => c.RecordNumber == number && c.Version == 1));
        await _fileStore.Received(1).SaveAsync(
            Arg.Any<Stream>(), $"acta-{number}-v1.pdf", "application/pdf", Arg.Any<CancellationToken>());
        produced.ShouldNotBeNull();
        produced.FileName.ShouldBe($"acta-{number}-v1.pdf");
        produced.ContentType.ShouldBe("application/pdf");
        produced.StorageKey.ShouldBe("acta-key");
        produced.CreatedById.ShouldBe(UserId);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsFileStoreUnavailable_WhenTheStoreCannotSave()
    {
        var raffle = SetupRaffleToComplete();
        _operators.ResolveNamesAsync(default!, default).ReturnsForAnyArgs(new Dictionary<Guid, string?>());
        _renderer.Render(Arg.Any<NotarialRecordContent>()).Returns([1]);
        _fileStore.SaveAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs<StoredFileLocation>(_ => throw new FileStoreUnavailableException("caído", new IOException()));
        SetupRepositoryRunsProduce(FactsFor(raffle), afterwards: null);

        var result = await _sut.CompleteAsync(EventId, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("FILE_STORE_UNAVAILABLE");
        await _fileStore.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task CompleteAsync_DiscardsTheSavedBytes_WhenTheWriteFailsAfterSaving()
    {
        var raffle = SetupRaffleToComplete();
        _operators.ResolveNamesAsync(default!, default).ReturnsForAnyArgs(new Dictionary<Guid, string?>());
        _renderer.Render(Arg.Any<NotarialRecordContent>()).Returns([1]);
        _fileStore.SaveAsync(default!, default!, default!, default).ReturnsForAnyArgs(Location("orphan-key"));
        SetupRepositoryRunsProduce(FactsFor(raffle), afterwards: new BaseError("Fallo de base de datos.", "DB_ERROR", ErrorKind.Internal));

        var result = await _sut.CompleteAsync(EventId, UserId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        await _fileStore.Received(1).DeleteAsync("orphan-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_DiscardsTheSavedBytes_WhenTheWriteThrowsAfterSaving()
    {
        var raffle = SetupRaffleToComplete();
        var facts = FactsFor(raffle);
        _operators.ResolveNamesAsync(default!, default).ReturnsForAnyArgs(new Dictionary<Guid, string?>());
        _renderer.Render(Arg.Any<NotarialRecordContent>()).Returns([1]);
        _fileStore.SaveAsync(default!, default!, default!, default).ReturnsForAnyArgs(Location("cancelled-key"));
        _repository.CompleteAsync(default, default, default, default!, default).ReturnsForAnyArgs<Task<UnitResult<BaseError>>>(async call =>
        {
            await call.ArgAt<Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>>>(3)(facts, 1, null);
            throw new OperationCanceledException();
        });

        await Should.ThrowAsync<OperationCanceledException>(() => _sut.CompleteAsync(EventId, UserId, CancellationToken.None));

        await _fileStore.Received(1).DeleteAsync("cancelled-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_NotifiesNobody_WhenTheWriteCommits()
    {
        SetupCommittedCompletion();

        await _sut.CompleteAsync(EventId, UserId, CancellationToken.None);

        // Los correos van aparte, en RaffleWinnerNotificationWorker (A31).
        _winnerNotifier.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task NotifyWinnersAsync_NotifiesEachLiveDrawInPrizeOrderAndRecordsItUncancelled_WhenTheRaffleWasCompleted()
    {
        var (first, second, voided) = SetupCommittedCompletion();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.NotThrowAsync(() => _sut.NotifyWinnersAsync(EventId, cancelled.Token));

        // Las relecturas después de confirmar no se cancelan: una cancelación ahí dejaría a todos sin correo.
        await _repository.Received(1).GetWithRoundsByEventIdAsync(EventId, CancellationToken.None);
        await _eventRepository.Received(1).GetByIdAsync(EventId, CancellationToken.None);
        var notified = _winnerNotifier.ReceivedCalls().Select(c => ((PrizeDraw)c.GetArguments()[3]!).Id).ToList();
        notified.ShouldBe([first.Id, second.Id]);
        await _repository.Received(1).UpdateWinnerNotificationAsync(
            first.Id, QuoteNotificationStatus.Sent, "batch", CancellationToken.None);
        await _repository.Received(1).UpdateWinnerNotificationAsync(
            second.Id, QuoteNotificationStatus.Sent, "batch", CancellationToken.None);
        await _repository.DidNotReceive().UpdateWinnerNotificationAsync(
            voided.Id, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_NotifiesNobody_WhenTheWriteFails()
    {
        SetupCommittedCompletion();
        _repository.CompleteAsync(default, default, default, default!, default)
            .ReturnsForAnyArgs(UnitResult.Failure<BaseError>(RaffleErrors.Completed()));

        await _sut.CompleteAsync(EventId, UserId, CancellationToken.None);

        _winnerNotifier.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>
    /// Un sorteo que completa sin acta que guardar, con dos premios sorteados en vivo —el segundo creado
    /// después— y un tercero cuyo único sorteo se anuló.
    /// </summary>
    private (PrizeDraw First, PrizeDraw Second, PrizeDraw Voided) SetupCommittedCompletion()
    {
        SetupRaffleToComplete();
        _repository.CompleteAsync(default, default, default, default!, default).ReturnsForAnyArgs(UnitResult.Success<BaseError>());

        var round = RaffleWithRoundsTests.Round(1);
        var now = DateTimeOffset.UtcNow;
        var prizes = new[]
        {
            RaffleWithRoundsTests.PrizeIn(round.Id, now),
            RaffleWithRoundsTests.PrizeIn(round.Id, now.AddMinutes(1)),
            RaffleWithRoundsTests.PrizeIn(round.Id, now.AddMinutes(2)),
        };
        var winner = DrawFixtures.Attendee("Ana");
        var first = DrawFixtures.LiveDraw(prizes[0].Id, winner);
        var second = DrawFixtures.LiveDraw(prizes[1].Id, winner);
        var voided = DrawFixtures.VoidedDraw(prizes[2].Id, winner, PrizeDrawVoidCode.Absent);

        // Los sorteos llegan desordenados: el orden es el de los premios, no el de la lista.
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.Completed, round with { Prizes = prizes }) with
        {
            Draws = [voided, second, first],
        }));
        _eventRepository.GetByIdAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(new EventWithCities(Event(), [], []))));
        _winnerNotifier.NotifyWonAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(QuoteNotificationOutcome.Sent("batch"));

        return (first, second, voided);
    }

    private static Event Event()
    {
        var now = DateTimeOffset.UtcNow;
        return new Event(
            id: EventId,
            type: EventType.Feria,
            name: "Feria",
            startDate: new DateOnly(2026, 3, 12),
            endDate: new DateOnly(2026, 3, 14),
            status: EventStatus.InProgress,
            language: AllowedLanguage.Espanol,
            visibility: EventVisibility.Private,
            seatingLayout: null,
            externalUrl: null,
            description: null,
            invitedCount: null,
            estimatedBudget: null,
            totalCost: null,
            cancellationReason: null,
            createdAt: now, updatedAt: now, createdById: UserId, updatedById: UserId, deletedAt: null);
    }

    #endregion

    #region ReissueAsync

    private static readonly RaffleAuthorities Corrected =
        new("Ana Notaria", "001-1", "Luis Testigo", "002-2", "Eva Testigo", "003-33");

    [Fact]
    public async Task ReissueAsync_ReturnsEventNotFound_WhenTheEventDoesNotExist()
    {
        SetupEventStatus(Maybe<string>.None);

        var result = await _sut.ReissueAsync(EventId, Corrected, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("EVENT_NOT_FOUND");
        await _repository.DidNotReceiveWithAnyArgs().ReissueAsync(default, default!, default, default, default!, default);
    }

    [Fact]
    public async Task ReissueAsync_ReturnsRaffleNotFound_WhenTheEventHasNoRaffle()
    {
        var result = await _sut.ReissueAsync(EventId, Corrected, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_FOUND");
        await _repository.DidNotReceiveWithAnyArgs().ReissueAsync(default, default!, default, default, default!, default);
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task ReissueAsync_ReachesTheRepositoryWithTheAuthorities_WhenTheEventIsNoLongerEditable(string status)
    {
        // El notario puede ver el error días después, con el evento ya cerrado (spec 00 §5).
        SetupEventStatus(Maybe.From(status));
        var raffle = SetupRaffleToComplete();
        _repository.ReissueAsync(default, default!, default, default, default!, default)
            .ReturnsForAnyArgs(UnitResult.Failure<BaseError>(RaffleErrors.NotCompleted()));

        var result = await _sut.ReissueAsync(EventId, Corrected, UserId, CancellationToken.None);

        result.Error.Code.ShouldBe("RAFFLE_NOT_COMPLETED");
        await _repository.Received(1).ReissueAsync(
            raffle.Id, Corrected, UserId, Arg.Any<DateTimeOffset>(), Arg.Any<Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReissueAsync_RendersAndSavesTheVersionItIsHanded_WhenTheRepositoryProducesIt()
    {
        var raffle = SetupRaffleToComplete();
        var facts = FactsFor(raffle);
        var replaces = new NotarialRecordReplaces(1, DateTimeOffset.UtcNow.AddDays(-1));
        _operators.ResolveNamesAsync(default!, default).ReturnsForAnyArgs(new Dictionary<Guid, string?>());
        _renderer.Render(Arg.Any<NotarialRecordContent>()).Returns([1]);
        _fileStore.SaveAsync(default!, default!, default!, default).ReturnsForAnyArgs(Location("v2-key"));
        StoredFile? produced = null;
        _repository.ReissueAsync(default, default!, default, default, default!, default).ReturnsForAnyArgs(async call =>
        {
            var produce = call.ArgAt<Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>>>(4);
            produced = (await produce(facts, 2, replaces)).Value;
            return UnitResult.Failure<BaseError>(RaffleErrors.NotCompleted());
        });

        await _sut.ReissueAsync(EventId, Corrected, UserId, CancellationToken.None);

        var number = NotarialRecordContent.NumberOf(raffle.Id);
        _renderer.Received(1).Render(Arg.Is<NotarialRecordContent>(c => c.Version == 2 && c.Replaces == replaces));
        await _fileStore.Received(1).SaveAsync(
            Arg.Any<Stream>(), $"acta-{number}-v2.pdf", "application/pdf", Arg.Any<CancellationToken>());
        produced.ShouldNotBeNull();
        produced.CreatedById.ShouldBe(UserId);
    }

    [Fact]
    public async Task ReissueAsync_DiscardsTheSavedBytes_WhenTheWriteFailsAfterSaving()
    {
        var raffle = SetupRaffleToComplete();
        var facts = FactsFor(raffle);
        _operators.ResolveNamesAsync(default!, default).ReturnsForAnyArgs(new Dictionary<Guid, string?>());
        _renderer.Render(Arg.Any<NotarialRecordContent>()).Returns([1]);
        _fileStore.SaveAsync(default!, default!, default!, default).ReturnsForAnyArgs(Location("orphan-v2"));
        _repository.ReissueAsync(default, default!, default, default, default!, default).ReturnsForAnyArgs(async call =>
        {
            await call.ArgAt<Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>>>(4)(
                facts, 2, new NotarialRecordReplaces(1, DateTimeOffset.UtcNow));
            return UnitResult.Failure(new BaseError("Fallo de base de datos.", "DB_ERROR", ErrorKind.Internal));
        });

        var result = await _sut.ReissueAsync(EventId, Corrected, UserId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        await _fileStore.Received(1).DeleteAsync("orphan-v2", Arg.Any<CancellationToken>());
    }

    /// <summary>Un sorteo en curso guardado, que el servicio encuentra por el evento.</summary>
    private Raffle SetupRaffleToComplete()
    {
        var raffle = Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow).Start(null, UserId, DateTimeOffset.UtcNow).Value;
        SetupRaffle(Maybe.From(raffle));
        return raffle;
    }

    /// <summary>
    /// El repositorio corre <c>produce</c> con estos hechos y después falla con
    /// <paramref name="afterwards"/>, o con el error de <c>produce</c> si este falló.
    /// </summary>
    private void SetupRepositoryRunsProduce(NotarialRecordFacts facts, BaseError? afterwards) =>
        _repository.CompleteAsync(default, default, default, default!, default).ReturnsForAnyArgs(async call =>
        {
            var produce = call.ArgAt<Func<NotarialRecordFacts, int, NotarialRecordReplaces?, Task<Result<StoredFile, BaseError>>>>(3);
            var produced = await produce(facts, 1, null);
            if (produced.IsFailure)
                return UnitResult.Failure(produced.Error);

            return afterwards is null ? UnitResult.Success<BaseError>() : UnitResult.Failure(afterwards);
        });

    /// <summary>Lo que el repositorio leería bajo el bloqueo: el sorteo ya completado.</summary>
    private static NotarialRecordFacts FactsFor(Raffle raffle) => new(
        new RaffleWithRounds(raffle with { State = RaffleState.Completed, CompletedAt = DateTimeOffset.UtcNow, CompletedById = UserId }, []),
        [],
        new NotarialRecordEvent("Roadshow", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), []));

    private static StoredFileLocation Location(string key) => new("filesystem", key, 4, "sha256:abc");

    #endregion

    #region ReinstateAttendeeAsync

    [Fact]
    public async Task ReinstateAttendeeAsync_ReturnsUpdatedRaffle_WhenRepositorySucceeds()
    {
        var attendeeId = Guid.NewGuid();
        var raffle = WithRounds(RaffleState.InProgress);
        var updatedWithRounds = raffle with { ReinstatedAttendeeIds = [attendeeId] };
        SetupRaffle(Maybe.From(raffle.Raffle));
        SetupRaffleWithRounds(Maybe.From(updatedWithRounds));
        _repository.ReinstateAttendeeAsync(raffle.Raffle.Id, attendeeId, UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Success<BaseError>());

        var result = await _sut.ReinstateAttendeeAsync(EventId, attendeeId, UserId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Raffle.ReinstatedAttendeeIds.ShouldBe([attendeeId]);
    }

    [Fact]
    public async Task ReinstateAttendeeAsync_ReturnsConflict_WhenAttendeeHasNoVoidedDraws()
    {
        var attendeeId = Guid.NewGuid();
        var raffle = WithRounds(RaffleState.InProgress);
        SetupRaffle(Maybe.From(raffle.Raffle));
        _repository.ReinstateAttendeeAsync(raffle.Raffle.Id, attendeeId, UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(UnitResult.Failure(RaffleErrors.AttendeeNotVoided()));

        var result = await _sut.ReinstateAttendeeAsync(EventId, attendeeId, UserId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("RAFFLE_ATTENDEE_NOT_VOIDED");
    }

    #endregion

    private void SetupDraftRaffle(out Raffle raffle)
    {
        raffle = Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow);
        SetupRaffle(Maybe.From(raffle));
    }

    private static SetAttendeeOverrideDto OverrideDto(
        string email, string decision = AttendeeOverrideDecision.Included, string? note = null) =>
        new(EventId, email, decision, note, UserId);

    private static RaffleWithRounds WithRounds(params RaffleRound[] rounds) =>
        WithRounds(RaffleState.Draft, rounds);

    private static RaffleWithRounds WithRounds(string state, params RaffleRound[] rounds) => new(
        Raffle.CreateDraft(EventId, UserId, DateTimeOffset.UtcNow) with { State = state },
        rounds);

    private static UpdateRaffleDto UpdateDto() => new(
        EventId: EventId,
        VisualStyle: RaffleVisualStyle.SequentialNames,
        ShowPrizeOnScreen: true,
        AnimationSeconds: 20,
        RequiresPresenceConfirmation: false,
        OnePrizePerPerson: true,
        OnePrizePerCompany: false,
        Authorities: RaffleAuthorities.Blank,
        PickupInstructions: null,
        UpdatedById: UserId);

    private static RaffleWithRounds WithPrize()
    {
        var round = RaffleWithRoundsTests.Round(1);
        return WithRounds(round with { Prizes = [RaffleWithRoundsTests.PrizeIn(round.Id, DateTimeOffset.UtcNow)] });
    }

    private static Attendee Person(string email, string firstName = "Nombre", string lastName = "Apellido") => new(
        email, firstName, lastName, "Agencia", Guid.NewGuid(), AttendeeOrigin.Invited, DateTimeOffset.UtcNow);

    private static Attendee[] Door(int count) =>
        Enumerable.Range(0, count).Select(i => Person($"persona{i}@agencia.com")).ToArray();

    private void SetupDoor(params Attendee[] attendees) =>
        SetupAttendance(new RecentAttendance(AttendanceStatus.Ok, DateTimeOffset.UtcNow, attendees));

    private void SetupAttendance(RecentAttendance attendance) =>
        _attendance.GetAsync(EventId, Arg.Any<CancellationToken>()).Returns(attendance);

    private void SetupOverrides(params AttendeeOverride[] overrides) =>
        _repository.ListOverridesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<AttendeeOverride>, BaseError>(overrides));

    private void SetupLiveDoor(params Attendee[] attendees) =>
        _liveAttendance.GetCheckedInAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<Attendee>, BaseError>(attendees));

    private void SetupMailability(Func<Guid, string?> reasonOf) =>
        _mailability.CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result.Success<IReadOnlyDictionary<Guid, string?>, BaseError>(
                callInfo.Arg<IReadOnlyCollection<Guid>>().ToDictionary(id => id, reasonOf)));

    /// <summary>
    /// El repositorio arranca como el de verdad: le pasa al congelado el sorteo y las decisiones
    /// manuales que leyó bajo el bloqueo. Devuelve lo que se habría escrito, o nulo si nada.
    /// </summary>
    private Func<RaffleFreeze?> SetupStart(Raffle locked, params AttendeeOverride[] overrides)
    {
        RaffleFreeze? written = null;
        _repository.StartAsync(
                locked.Id,
                Arg.Any<Func<Raffle, IReadOnlyList<AttendeeOverride>, Result<RaffleFreeze, BaseError>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var freeze = callInfo.Arg<Func<Raffle, IReadOnlyList<AttendeeOverride>, Result<RaffleFreeze, BaseError>>>();
                var decided = freeze(locked, overrides);
                if (decided.IsFailure)
                    return UnitResult.Failure(decided.Error);

                written = decided.Value;
                return UnitResult.Success<BaseError>();
            });
        SetupRaffleWithRounds(Maybe.From(WithRounds(RaffleState.InProgress)));
        return () => written;
    }

    private async Task AssertNothingStartedAsync() =>
        await _repository.DidNotReceive().StartAsync(
            Arg.Any<Guid>(),
            Arg.Any<Func<Raffle, IReadOnlyList<AttendeeOverride>, Result<RaffleFreeze, BaseError>>>(),
            Arg.Any<CancellationToken>());

    private static RaffleAttendee Frozen(string email, string firstName, string lastName) => new(
        Guid.NewGuid(), email, Guid.NewGuid(), firstName, lastName, "Agencia", DateTimeOffset.UtcNow,
        Qualified: true, ExclusionReason: null, ManualDecision: null, ManualDecidedById: null,
        ManualDecidedAt: null, ManualNote: null, OverruledReason: null);

    private void SetupCrmDown() =>
        _mailability.CheckAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyDictionary<Guid, string?>, BaseError>(RaffleErrors.CrmUnavailable()));

    private void SetupAwards(Maybe<AwardsList> list) =>
        _awards.GetInForceAsync(Arg.Any<CancellationToken>()).Returns(list);

    private void SetupEventStatus(Maybe<string> status) =>
        _eventRepository.GetStatusAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<string>, BaseError>(status));

    private void SetupRaffle(Maybe<Raffle> raffle) =>
        _repository.GetByEventIdAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<Raffle>, BaseError>(raffle));

    private void SetupRaffleWithRounds(Maybe<RaffleWithRounds> raffle) =>
        _repository.GetWithRoundsByEventIdAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<RaffleWithRounds>, BaseError>(raffle));
}
