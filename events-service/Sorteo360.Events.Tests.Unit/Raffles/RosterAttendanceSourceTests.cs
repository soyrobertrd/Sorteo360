using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Raffles;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class RosterAttendanceSourceTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 12, 18, 20, 0, 0, TimeSpan.Zero);

    private readonly IRaffleParticipantRepository _repository = Substitute.For<IRaffleParticipantRepository>();

    private RosterAttendanceSource CreateSut() => new(_repository);

    [Fact]
    public async Task GetCheckedInAsync_MapsOnlyCheckedInParticipantsUsingDepartmentFallback_WhenRepositorySucceeds()
    {
        var checkedIn = new RaffleParticipant(
            id: Guid.NewGuid(),
            eventId: EventId,
            fullName: "Ana María Pérez",
            emailNormalized: "ana@empresa.do",
            company: null,
            department: "Finanzas",
            employeeCode: "EMP-001",
            phone: null,
            qrCode: "QR-ANA",
            checkedIn: true,
            checkedInAt: Now,
            source: "excel",
            createdAt: Now.AddHours(-2),
            updatedAt: Now);

        var pending = new RaffleParticipant(
            id: Guid.NewGuid(),
            eventId: EventId,
            fullName: "Luis Gómez",
            emailNormalized: "luis@empresa.do",
            company: "Empresa SA",
            department: "TI",
            employeeCode: "EMP-002",
            phone: null,
            qrCode: "QR-LUIS",
            checkedIn: false,
            checkedInAt: null,
            source: "excel",
            createdAt: Now.AddHours(-2),
            updatedAt: Now.AddHours(-2));

        _repository.ListByEventIdAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<RaffleParticipant>, BaseError>([checkedIn, pending]));

        var result = await CreateSut().GetCheckedInAsync(EventId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var attendee = result.Value.ShouldHaveSingleItem();
        attendee.EmailNormalized.ShouldBe("ana@empresa.do");
        attendee.FirstName.ShouldBe("Ana");
        attendee.LastName.ShouldBe("María Pérez");
        attendee.Company.ShouldBe("Finanzas");
        attendee.ContactId.ShouldBeNull();
        attendee.Origin.ShouldBe(AttendeeOrigin.Invited);
        attendee.CheckedInAt.ShouldBe(Now);
    }

    [Fact]
    public async Task GetCheckedInAsync_ReturnsFailure_WhenRepositoryFails()
    {
        var error = new BaseError("DB_ERROR", "Fallo al leer el padrón.", ErrorKind.Internal);
        _repository.ListByEventIdAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<RaffleParticipant>, BaseError>(error));

        var result = await CreateSut().GetCheckedInAsync(EventId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }
}

