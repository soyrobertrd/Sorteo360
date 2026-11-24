using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Tests.Unit.Neighbours;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.CheckIns;

/// <summary>
/// Las decisiones de la puerta (spec <c>09</c> §3 y §4): de quién es el código, si este escaneo hizo
/// el check-in o lo encontró hecho, y qué deja escrito una deshecha.
/// </summary>
public sealed class CheckInServiceTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid RegistrationId = Guid.NewGuid();
    private static readonly Guid DoorStaffId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 18, 42, 11, TimeSpan.Zero);
    private const string QrCode = "q8ZrAbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_A";
    private const string FirstName = "María";
    private const string LastName = "García";
    private const string Company = "Viajes del Caribe SRL";
    private const string Reference = "REF-2026-00427";

    private readonly ICheckInRepository _repository = Substitute.For<ICheckInRepository>();
    private readonly RecordingLogger<CheckInService> _logger = new();
    private readonly CheckInService _service;

    public CheckInServiceTests()
    {
        _service = new CheckInService(_repository, new FixedClock(Now), _logger);
    }

    [Fact]
    public async Task CheckInAsync_ReturnsCodeNotFound_WithoutStamping_WhenNoRegistrationHasTheCode()
    {
        GivenCodeBelongsTo(Maybe<CheckInTarget>.None);

        var result = await _service.CheckInAsync(EventId, QrCode, DoorStaffId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CheckInCodeNotFoundError.ErrorCode);
        await _repository.DidNotReceiveWithAnyArgs().StampAsync(default, default, default, default);
    }

    [Fact]
    public async Task CheckInAsync_ReturnsWrongEvent_WithoutStamping_WhenTheCodeIsOnAnotherEvent()
    {
        GivenCodeBelongsTo(Maybe.From(new CheckInTarget(RegistrationId, Guid.NewGuid())));

        var result = await _service.CheckInAsync(EventId, QrCode, DoorStaffId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CheckInWrongEventError.ErrorCode);
        await _repository.DidNotReceiveWithAnyArgs().StampAsync(default, default, default, default);
    }

    [Fact]
    public async Task CheckInAsync_ReturnsANewCheckIn_StampedNowByTheCaller_WhenTheStampChangedTheRow()
    {
        GivenCodeBelongsTo(Maybe.From(new CheckInTarget(RegistrationId, EventId)));
        GivenStamp(changed: true, checkedInAt: Now, checkedInById: DoorStaffId);

        var result = await _service.CheckInAsync(EventId, QrCode, DoorStaffId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new CheckIn(
            RegistrationId, FirstName, LastName, Company, Reference,
            Now, DoorStaffId, AlreadyCheckedIn: false));
        await _repository.Received(1).StampAsync(RegistrationId, Now, DoorStaffId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckInAsync_ReturnsTheFirstCheckIn_AsAlreadyCheckedIn_WhenTheStampChangedNothing()
    {
        var firstAt = Now.AddMinutes(-20);
        var firstBy = Guid.NewGuid();
        GivenCodeBelongsTo(Maybe.From(new CheckInTarget(RegistrationId, EventId)));
        GivenStamp(changed: false, checkedInAt: firstAt, checkedInById: firstBy);

        var result = await _service.CheckInAsync(EventId, QrCode, DoorStaffId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AlreadyCheckedIn.ShouldBeTrue();
        result.Value.CheckedInAt.ShouldBe(firstAt);
        result.Value.CheckedInById.ShouldBe(firstBy);
    }

    [Fact]
    public async Task CheckInByRegistrationAsync_ReturnsRegistrationNotFound_WithoutStamping_WhenNoRegistrationHasTheId()
    {
        GivenRegistrationIs(Maybe<CheckInTarget>.None);

        var result = await _service.CheckInByRegistrationAsync(EventId, RegistrationId, DoorStaffId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RegistrationNotFoundError.ErrorCode);
        await _repository.DidNotReceiveWithAnyArgs().StampAsync(default, default, default, default);
    }

    [Fact]
    public async Task CheckInByRegistrationAsync_ReturnsWrongEvent_WithoutStamping_WhenTheRegistrationIsOnAnotherEvent()
    {
        GivenRegistrationIs(Maybe.From(new CheckInTarget(RegistrationId, Guid.NewGuid())));

        var result = await _service.CheckInByRegistrationAsync(EventId, RegistrationId, DoorStaffId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(CheckInWrongEventError.ErrorCode);
        await _repository.DidNotReceiveWithAnyArgs().StampAsync(default, default, default, default);
    }

    [Fact]
    public async Task CheckInByRegistrationAsync_ReturnsANewCheckIn_StampedNowByTheCaller_WhenTheStampChangedTheRow()
    {
        GivenRegistrationIs(Maybe.From(new CheckInTarget(RegistrationId, EventId)));
        GivenStamp(changed: true, checkedInAt: Now, checkedInById: DoorStaffId);

        var result = await _service.CheckInByRegistrationAsync(EventId, RegistrationId, DoorStaffId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new CheckIn(
            RegistrationId, FirstName, LastName, Company, Reference,
            Now, DoorStaffId, AlreadyCheckedIn: false));
        await _repository.Received(1).StampAsync(RegistrationId, Now, DoorStaffId, Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().FindByQrCodeAsync(default!, default);
    }

    [Fact]
    public async Task CheckInByRegistrationAsync_ReturnsTheFirstCheckIn_AsAlreadyCheckedIn_WhenTheStampChangedNothing()
    {
        var firstAt = Now.AddMinutes(-20);
        var firstBy = Guid.NewGuid();
        GivenRegistrationIs(Maybe.From(new CheckInTarget(RegistrationId, EventId)));
        GivenStamp(changed: false, checkedInAt: firstAt, checkedInById: firstBy);

        var result = await _service.CheckInByRegistrationAsync(EventId, RegistrationId, DoorStaffId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AlreadyCheckedIn.ShouldBeTrue();
        result.Value.CheckedInAt.ShouldBe(firstAt);
        result.Value.CheckedInById.ShouldBe(firstBy);
    }

    [Fact]
    public async Task UndoAsync_LogsOneLineWithTheFiveValues_WhenItClearedACheckIn()
    {
        var clearedAt = Now.AddMinutes(-5);
        var clearedBy = Guid.NewGuid();
        GivenClearance(Maybe.From(new CheckInClearance(new ClearedCheckIn(clearedAt, clearedBy))));

        var result = await _service.UndoAsync(EventId, RegistrationId, DoorStaffId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldContain(RegistrationId.ToString());
        entry.Message.ShouldContain(EventId.ToString());
        entry.Message.ShouldContain(clearedAt.ToString(CultureInfo.InvariantCulture));
        entry.Message.ShouldContain(clearedBy.ToString());
        entry.Message.ShouldContain(DoorStaffId.ToString());
        await _repository.Received(1).ClearAsync(EventId, RegistrationId, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UndoAsync_SucceedsWithoutLogging_WhenThereWasNoCheckInToClear()
    {
        GivenClearance(Maybe.From(new CheckInClearance(Cleared: null)));

        var result = await _service.UndoAsync(EventId, RegistrationId, DoorStaffId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task UndoAsync_ReturnsRegistrationNotFound_WhenTheRegistrationIsNotOnTheEvent()
    {
        GivenClearance(Maybe<CheckInClearance>.None);

        var result = await _service.UndoAsync(EventId, RegistrationId, DoorStaffId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RegistrationNotFoundError.ErrorCode);
        _logger.Entries.ShouldBeEmpty();
    }

    /// <summary>El código <see cref="QrCode"/> es de esa inscripción, o de ninguna.</summary>
    private void GivenCodeBelongsTo(Maybe<CheckInTarget> target) =>
        _repository.FindByQrCodeAsync(QrCode, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CheckInTarget>, BaseError>(target));

    /// <summary>La inscripción <see cref="RegistrationId"/> es de ese evento, o no existe.</summary>
    private void GivenRegistrationIs(Maybe<CheckInTarget> target) =>
        _repository.FindByIdAsync(RegistrationId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CheckInTarget>, BaseError>(target));

    /// <summary>El sello devuelve la fila de <see cref="RegistrationId"/> con ese check-in.</summary>
    private void GivenStamp(bool changed, DateTimeOffset checkedInAt, Guid checkedInById) =>
        _repository.StampAsync(RegistrationId, Arg.Any<DateTimeOffset>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<CheckInStamp, BaseError>(new CheckInStamp(
                changed, RegistrationId, FirstName, LastName, Company, Reference,
                checkedInAt, checkedInById)));

    /// <summary>Deshacer el check-in de <see cref="RegistrationId"/> en <see cref="EventId"/> da eso.</summary>
    private void GivenClearance(Maybe<CheckInClearance> clearance) =>
        _repository.ClearAsync(EventId, RegistrationId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Maybe<CheckInClearance>, BaseError>(clearance));

    /// <summary>Un reloj clavado, para poder afirmar las horas que se sellan.</summary>
    private sealed class FixedClock : TimeProvider
    {
        public FixedClock(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
