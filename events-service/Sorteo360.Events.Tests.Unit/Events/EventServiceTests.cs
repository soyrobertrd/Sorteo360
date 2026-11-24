using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Events.Tests.Unit.Shared;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Events;

public sealed class EventServiceTests
{
    private readonly IEventRepository _repository;
    private readonly TestTimeProvider _clock = new();
    private readonly EventService _sut;

    public EventServiceTests()
    {
        _repository = Substitute.For<IEventRepository>();
        _sut = new EventService(_repository, _clock);
    }

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ThrowsArgumentNullException_WhenDtoIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.CreateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ReturnsSuccessWithEventId_WhenRepositorySucceeds()
    {
        _repository
            .CreateAsync(Arg.Any<Event>(), Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result.Success<Guid, BaseError>(callInfo.Arg<Event>().Id));

        var result = await _sut.CreateAsync(CreateDto(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_ReturnsFailure_WhenRepositoryFails()
    {
        var error = new BaseError("Error de base de datos", "DB_ERROR");
        _repository
            .CreateAsync(Arg.Any<Event>(), Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid, BaseError>(error));

        var result = await _sut.CreateAsync(CreateDto(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DB_ERROR");
    }

    [Fact]
    public async Task CreateAsync_ReturnsAPersistedEventInPlanning_WhenDtoIsValid()
    {
        Event? capturedEvent = null;
        _repository
            .CreateAsync(Arg.Any<Event>(), Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedEvent = callInfo.Arg<Event>();
                return Result.Success<Guid, BaseError>(capturedEvent.Id);
            });

        await _sut.CreateAsync(CreateDto(), CancellationToken.None);

        capturedEvent.ShouldNotBeNull();
        capturedEvent!.Status.ShouldBe(EventStatus.Planning);
    }

    [Fact]
    public async Task CreateAsync_ReturnsAPersistedEventWithNoTotalCost_WhenDtoIsValid()
    {
        // TotalCost es el costo total final del evento: se calcula luego de aprobar las
        // cotizaciones, por lo que nunca se setea al crear (no viene en el DTO de entrada).
        Event? capturedEvent = null;
        _repository
            .CreateAsync(Arg.Any<Event>(), Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedEvent = callInfo.Arg<Event>();
                return Result.Success<Guid, BaseError>(capturedEvent.Id);
            });

        await _sut.CreateAsync(CreateDto(), CancellationToken.None);

        capturedEvent.ShouldNotBeNull();
        capturedEvent!.TotalCost.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsAPersistedEventWithBudgetAndDescription_WhenDtoHasThem()
    {
        // El presupuesto estimado sí viene del DTO, a diferencia de TotalCost: lo escribe la OPT
        // al planificar y los dos conviven en el evento sin regla que ate uno al otro.
        Event? capturedEvent = null;
        _repository
            .CreateAsync(Arg.Any<Event>(), Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedEvent = callInfo.Arg<Event>();
                return Result.Success<Guid, BaseError>(capturedEvent.Id);
            });

        var budget = new Money(125_000m, Currency.EUR);
        await _sut.CreateAsync(CreateDto("Roadshow por tres ciudades.", budget), CancellationToken.None);

        capturedEvent.ShouldNotBeNull();
        capturedEvent!.Description.ShouldBe("Roadshow por tres ciudades.");
        capturedEvent.EstimatedBudget.ShouldBe(budget);
        capturedEvent.TotalCost.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_ReturnsInvalid_WhenBudgetAmountIsNotPositive(int amount)
    {
        // Quien llama al servicio sin pasar por HTTP también merece un rechazo con código: sin
        // esto la restricción de la base saldría como 500.
        var result = await _sut.CreateAsync(
            CreateDto(estimatedBudget: new Money(amount, Currency.USD)), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_BUDGET_AMOUNT_INVALID");

        await _repository.DidNotReceive()
            .CreateAsync(Arg.Any<Event>(), Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ReturnsInvalid_WhenBudgetAmountIsNotPositive()
    {
        var @event = CreateEventWithCities(EventStatus.Planning);
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.UpdateAsync(
            UpdateDto(@event.Event.Id, estimatedBudget: new Money(-1, Currency.USD)), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_BUDGET_AMOUNT_INVALID");

        await _repository.DidNotReceive()
            .UpdateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ReturnsAPersistedCityWithDisplayOrderOne_WhenDtoIsValid()
    {
        RoadshowCity? capturedCity = null;
        _repository
            .CreateAsync(Arg.Any<Event>(), Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedCity = callInfo.Arg<RoadshowCity>();
                return Result.Success<Guid, BaseError>(callInfo.Arg<Event>().Id);
            });

        await _sut.CreateAsync(CreateDto(), CancellationToken.None);

        capturedCity.ShouldNotBeNull();
        capturedCity!.DisplayOrder.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_ReturnsAPersistedCityLinkedToTheEvent_WhenDtoIsValid()
    {
        Event? capturedEvent = null;
        RoadshowCity? capturedCity = null;
        _repository
            .CreateAsync(Arg.Any<Event>(), Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedEvent = callInfo.Arg<Event>();
                capturedCity = callInfo.Arg<RoadshowCity>();
                return Result.Success<Guid, BaseError>(capturedEvent.Id);
            });

        await _sut.CreateAsync(CreateDto(), CancellationToken.None);

        capturedCity!.EventId.ShouldBe(capturedEvent!.Id);
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ReturnsNone_WhenRepositoryReturnsNone()
    {
        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.None));

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEventWithCities_WhenRepositoryReturnsValue()
    {
        var eventWithCities = CreateEventWithCities();
        _repository
            .GetByIdAsync(eventWithCities.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(eventWithCities)));

        var result = await _sut.GetByIdAsync(eventWithCities.Event.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeTrue();
        result.Value.Value.Event.Id.ShouldBe(eventWithCities.Event.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsFailure_WhenRepositoryFails()
    {
        var error = new BaseError("Error de base de datos", "DB_ERROR");
        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Maybe<EventWithCities>, BaseError>(error));

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("DB_ERROR");
    }

    #endregion

    #region ListAsync

    [Fact]
    public async Task ListAsync_ThrowsArgumentNullException_WhenFilterIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.ListAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_ReturnsTheRepositoryPage_WhenTheFilterIsValid()
    {
        var filter = new ListEventsDto(
            Type: null, Status: null, Active: null, Visibility: null,
            StartDateFrom: null, StartDateTo: null,
            SearchTerm: null,
            SortField: EventSortField.StartDate, SortOrder: SortOrder.Desc,
            Page: 1, PageSize: 20);

        var expected = new PagedResult<EventWithCover>([], 0, 1, 20);
        _repository
            .ListAsync(filter, Arg.Any<CancellationToken>())
            .Returns(Result.Success<PagedResult<EventWithCover>, BaseError>(expected));

        var result = await _sut.ListAsync(filter, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
        await _repository.Received(1).ListAsync(filter, Arg.Any<CancellationToken>());
    }

    #endregion

    #region StartAsync

    [Fact]
    public async Task StartAsync_ReturnsNone_WhenEventDoesNotExist()
    {
        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.None));

        var result = await _sut.StartAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeFalse();
        await _repository.DidNotReceive().StartAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStatus.InProgress)]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task StartAsync_ReturnsConflict_WhenEventIsNotPlanning(string status)
    {
        var @event = CreateEventWithCities(status);
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.StartAsync(@event.Event.Id, Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_STARTABLE");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        await _repository.DidNotReceive().StartAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_ReturnsTheRepositoryResult_WhenEventIsPlanning()
    {
        var @event = CreateEventWithCities(EventStatus.Planning);
        var eventId = @event.Event.Id;
        var startedById = Guid.NewGuid();

        _repository
            .GetByIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));
        _repository
            .StartAsync(eventId, startedById, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.StartAsync(eventId, startedById, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeTrue();
        await _repository.Received(1).StartAsync(eventId, startedById, Arg.Any<CancellationToken>());
    }

    #endregion

    #region CancelAsync

    [Fact]
    public async Task CancelAsync_ThrowsArgumentNullException_WhenReasonIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.CancelAsync(Guid.NewGuid(), null!, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CancelAsync_ReturnsNone_WhenEventDoesNotExist()
    {
        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.None));

        var result = await _sut.CancelAsync(Guid.NewGuid(), "Se pospone el roadshow.", Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeFalse();
        await _repository.DidNotReceive().CancelAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task CancelAsync_ReturnsConflict_WhenEventIsTerminal(string status)
    {
        var @event = CreateEventWithCities(status);
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.CancelAsync(@event.Event.Id, "Se pospone el roadshow.", Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_CANCELLABLE");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        await _repository.DidNotReceive().CancelAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStatus.Planning)]
    [InlineData(EventStatus.InProgress)]
    public async Task CancelAsync_ReturnsTheRepositoryResult_WhenEventIsNonTerminal(string status)
    {
        var @event = CreateEventWithCities(status);
        var eventId = @event.Event.Id;
        var cancelledById = Guid.NewGuid();
        const string reason = "Se pospone el roadshow.";

        _repository
            .GetByIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));
        _repository
            .CancelAsync(eventId, reason, cancelledById, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.CancelAsync(eventId, reason, cancelledById, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeTrue();
        await _repository.Received(1).CancelAsync(eventId, reason, cancelledById, Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ThrowsArgumentNullException_WhenDtoIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.UpdateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNone_WhenEventDoesNotExist()
    {
        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.None));

        var result = await _sut.UpdateAsync(UpdateDto(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeFalse();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task UpdateAsync_ReturnsConflict_WhenEventIsTerminal(string status)
    {
        var @event = CreateEventWithCities(status);
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.UpdateAsync(UpdateDto(@event.Event.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStatus.Planning)]
    [InlineData(EventStatus.InProgress)]
    public async Task UpdateAsync_SavesTheEditedEventAndReturnsTheRepositoryResult_WhenEventIsNonTerminal(string status)
    {
        var @event = CreateEventWithCities(status);
        var dto = UpdateDto(@event.Event.Id);
        var saved = CreateEventWithCities(status);

        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        Event? received = null;
        _repository
            .UpdateAsync(Arg.Do<Event>(e => received = e), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(saved)));

        var result = await _sut.UpdateAsync(dto, CancellationToken.None);

        // El repositorio recibe el evento que produjo la edición: cambia lo editable, el estado no.
        received.ShouldNotBeNull();
        received!.Id.ShouldBe(@event.Event.Id);
        received.Name.ShouldBe("Roadshow Editado");
        received.InvitedCount.ShouldBe(50);
        received.Status.ShouldBe(status);
        received.UpdatedAt.ShouldBe(_clock.GetUtcNow());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBeSameAs(saved);
    }

    #endregion

    #region CloseAsync

    [Fact]
    public async Task CloseAsync_ReturnsTheRepositoryAnswerWithoutReadingTheEvent_WhenTheEventIsNotClosable()
    {
        var eventId = Guid.NewGuid();
        _repository
            .CloseAsync(eventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Maybe<EventWithCities>, BaseError>(EventErrors.NotClosable()));

        var result = await _sut.CloseAsync(eventId, Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_CLOSABLE");
        await _repository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task CloseAsync_ReturnsSuccess_WhenValid()
    {
        var @event = CreateEventWithCities(EventStatus.InProgress);
        var eventId = @event.Event.Id;
        var closedById = Guid.NewGuid();
        _repository
            .CloseAsync(eventId, closedById, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.CloseAsync(eventId, closedById, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeTrue();
        await _repository.Received(1).CloseAsync(eventId, closedById, Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdateCityAsync

    [Fact]
    public async Task UpdateCityAsync_ThrowsArgumentNullException_WhenDtoIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _sut.UpdateCityAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateCityAsync_ReturnsNone_WhenEventDoesNotExist()
    {
        _repository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.None));

        var result = await _sut.UpdateCityAsync(UpdateCityDto(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeFalse();
        await _repository.DidNotReceive().UpdateCityAsync(Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCityAsync_ReturnsNone_WhenCityDoesNotExist()
    {
        var @event = CreateEventWithCities(EventStatus.Planning);
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.UpdateCityAsync(UpdateCityDto(@event.Event.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeFalse();
        await _repository.DidNotReceive().UpdateCityAsync(Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task UpdateCityAsync_ReturnsConflict_WhenEventIsTerminal(string status)
    {
        var @event = CreateEventWithCities(status);
        var cityId = @event.Cities[0].Id;
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.UpdateCityAsync(UpdateCityDto(@event.Event.Id, cityId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        await _repository.DidNotReceive().UpdateCityAsync(Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventType.Christmas)]
    [InlineData(EventType.Conference)]
    [InlineData(EventType.Other)]
    public async Task UpdateCityAsync_ReturnsUpdatedCity_WhenEventIsNotRoadshow(string type)
    {
        // Todo evento tiene una sola ciudad, y corregirla no cambia cuántas tiene (PGTI-335).
        var @event = CreateEventWithCities(EventStatus.Planning, type, cityCount: 1);
        var cityId = @event.Cities[0].Id;
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));
        _repository
            .UpdateCityAsync(Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<bool, BaseError>(true));

        var dto = UpdateCityDto(@event.Event.Id, cityId);
        var result = await _sut.UpdateCityAsync(dto, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.Name.ShouldBe(dto.Name);
        await _repository.Received(1).UpdateCityAsync(
            Arg.Is<RoadshowCity>(c => c.Id == cityId && c.Name == dto.Name),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task UpdateCityAsync_ReturnsNotEditable_WhenEventIsNotRoadshowAndTerminal(string status)
    {
        var @event = CreateEventWithCities(status, EventType.Feria, cityCount: 1);
        var cityId = @event.Cities[0].Id;
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));

        var result = await _sut.UpdateCityAsync(UpdateCityDto(@event.Event.Id, cityId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
        await _repository.DidNotReceive().UpdateCityAsync(Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCityAsync_ReturnsUpdatedCity_WhenValid()
    {
        var @event = CreateEventWithCities(EventStatus.Planning);
        var cityId = @event.Cities[0].Id;
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));
        _repository
            .UpdateCityAsync(Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<bool, BaseError>(true));

        var dto = UpdateCityDto(@event.Event.Id, cityId);
        var result = await _sut.UpdateCityAsync(dto, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeTrue();
        result.Value.Value.Id.ShouldBe(cityId);
        result.Value.Value.Name.ShouldBe(dto.Name);
        result.Value.Value.Country.ShouldBe(dto.Country);
        result.Value.Value.Date.ShouldBe(dto.Date);
        result.Value.Value.DisplayOrder.ShouldBe(dto.DisplayOrder);
        await _repository.Received(1).UpdateCityAsync(
            Arg.Is<RoadshowCity>(c => c.Id == cityId && c.Name == dto.Name),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCityAsync_ReturnsNone_WhenRepositoryReportsMissing()
    {
        // La ciudad existía al cargar pero otra petición la eliminó antes de persistir.
        var @event = CreateEventWithCities(EventStatus.Planning);
        var cityId = @event.Cities[0].Id;
        _repository
            .GetByIdAsync(@event.Event.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<EventWithCities>, BaseError>(Maybe.From(@event)));
        _repository
            .UpdateCityAsync(Arg.Any<RoadshowCity>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<bool, BaseError>(false));

        var result = await _sut.UpdateCityAsync(UpdateCityDto(@event.Event.Id, cityId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HasValue.ShouldBeFalse();
    }

    #endregion

    private static CreateEventDto CreateDto(string? description = null, Money? estimatedBudget = null)
    {
        return new CreateEventDto(
            Type: EventType.Roadshow,
            Name: "Test Roadshow",
            StartDate: new DateOnly(2026, 9, 1),
            EndDate: new DateOnly(2026, 9, 5),
            Language: "es",
            Visibility: EventVisibility.Private,
            SeatingLayout: null,
            ExternalUrl: null,
            Description: description,
            InvitedCount: null,
            EstimatedBudget: estimatedBudget,
            CityName: "Madrid",
            CityCountry: "Spain",
            CityTimeZone: CityTimeZone.Default,
            CreatedById: Guid.NewGuid());
    }

    private static UpdateEventDto UpdateDto(Guid id, Money? estimatedBudget = null)
    {
        return new UpdateEventDto(
            Id: id,
            Name: "Roadshow Editado",
            StartDate: new DateOnly(2026, 10, 1),
            EndDate: new DateOnly(2026, 10, 5),
            Language: "es",
            Visibility: null,
            SeatingLayout: null,
            ExternalUrl: null,
            Description: null,
            InvitedCount: 50,
            EstimatedBudget: estimatedBudget,
            UpdatedById: Guid.NewGuid());
    }

    private static UpdateCityDto UpdateCityDto(Guid eventId, Guid cityId)
    {
        return new UpdateCityDto(
            EventId: eventId,
            CityId: cityId,
            Name: "Sevilla",
            Country: "Spain",
            TimeZone: CityTimeZone.Create("Europe/Madrid").Value,
            Date: new DateOnly(2026, 9, 4),
            DisplayOrder: 2,
            UpdatedById: Guid.NewGuid());
    }

    private static EventWithCities CreateEventWithCities(string status = EventStatus.Planning)
    {
        return CreateEventWithCities(status, EventType.Roadshow, cityCount: 1);
    }

    private static EventWithCities CreateEventWithCities(string status, string type, int cityCount)
    {
        var now = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var @event = new Event(
            id: eventId,
            type: type,
            name: "Test Roadshow",
            startDate: new DateOnly(2026, 9, 1),
            endDate: new DateOnly(2026, 9, 5),
            status: status,
            language: AllowedLanguage.Espanol,
            visibility: EventVisibility.Private,
            seatingLayout: null,
            externalUrl: null,
            description: null,
            invitedCount: null,
            estimatedBudget: null,
            totalCost: null,
            cancellationReason: null,
            createdAt: now,
            updatedAt: now,
            createdById: userId,
            updatedById: userId,
            deletedAt: null);

        var cities = Enumerable.Range(1, cityCount)
            .Select(order => new RoadshowCity(
                id: Guid.NewGuid(),
                eventId: eventId,
                name: $"Ciudad {order}",
                country: "Spain",
                timeZone: "Europe/Madrid",
                date: null,
                displayOrder: order,
                createdAt: now,
                updatedAt: now,
                createdById: userId,
                updatedById: userId,
                deletedAt: null))
            .ToList();

        return new EventWithCities(@event, cities, []);
    }
}
