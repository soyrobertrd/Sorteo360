using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Convocatorias;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly.CircuitBreaker;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

/// <summary>
/// La puerta vista desde convocatorias (spec <c>01</c> §7): todo o nada. Una lista parcial no existe,
/// y una que no se pudo leer nunca llega como vacía, porque vacía es "nadie entró".
/// </summary>
public sealed class ConvocatoriasAttendanceSourceTests
{
    private static readonly DateTimeOffset CheckedInAt = new(2026, 10, 3, 18, 42, 11, TimeSpan.Zero);

    private readonly IConvocatoriasCheckInClient _client = Substitute.For<IConvocatoriasCheckInClient>();

    private ConvocatoriasAttendanceSource CreateSut() => new(_client, NullLogger<ConvocatoriasAttendanceSource>.Instance);

    [Fact]
    public async Task GetCheckedInAsync_ReturnsEachOriginAndTheRowAsSent_WhenConvocatoriasAnswers()
    {
        var contactId = Guid.NewGuid();
        Answer(
            Row("ana@agencia.com", "invited", contactId),
            Row("colega@agencia.com", "referred"),
            Row("libre@agencia.com", "walk_in"));

        var result = await CreateSut().GetCheckedInAsync(Guid.NewGuid(), CancellationToken.None);

        result.Value.ShouldBe(
        [
            new Attendee("ana@agencia.com", "María", "García", "Viajes del Caribe SRL", contactId, AttendeeOrigin.Invited, CheckedInAt),
            new Attendee("colega@agencia.com", "María", "García", "Viajes del Caribe SRL", null, AttendeeOrigin.Referred, CheckedInAt),
            new Attendee("libre@agencia.com", "María", "García", "Viajes del Caribe SRL", null, AttendeeOrigin.WalkIn, CheckedInAt),
        ]);
    }

    [Fact]
    public async Task GetCheckedInAsync_ReturnsAnEmptyList_WhenNobodyCheckedIn()
    {
        _client.ListCheckInsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ConvocatoriasCheckInList([]));

        var result = await CreateSut().GetCheckedInAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Invited")]
    [InlineData("walkIn")]
    [InlineData("guest")]
    [InlineData(null)]
    public async Task GetCheckedInAsync_ReturnsAttendanceUnavailable_WhenARowCarriesAnUnknownOrigin(string? origin)
    {
        Answer(Row("ana@agencia.com", "invited"), Row("otra@agencia.com", "invited") with { Origin = origin });

        var result = await CreateSut().GetCheckedInAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_UNAVAILABLE");
    }

    public static TheoryData<ConvocatoriasCheckIn> RowsMissingAField => new()
    {
        Row("ana@agencia.com", "invited") with { EmailNormalized = null },
        Row("ana@agencia.com", "invited") with { FirstName = null },
        Row("ana@agencia.com", "invited") with { LastName = null },
        Row("ana@agencia.com", "invited") with { CheckedInAt = null },
    };

    [Theory]
    [MemberData(nameof(RowsMissingAField))]
    public async Task GetCheckedInAsync_ReturnsAttendanceUnavailable_WhenARowMissesARequiredField(ConvocatoriasCheckIn row)
    {
        Answer(row);

        var result = await CreateSut().GetCheckedInAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_UNAVAILABLE");
    }

    [Fact]
    public async Task GetCheckedInAsync_ReturnsAttendanceUnavailable_WhenTheBodyIsNull()
    {
        _client.ListCheckInsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ConvocatoriasCheckInList?)null);

        var result = await CreateSut().GetCheckedInAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_UNAVAILABLE");
    }

    [Fact]
    public async Task GetCheckedInAsync_ReturnsAttendanceUnavailable_WhenTheBodyHasNoItems()
    {
        _client.ListCheckInsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ConvocatoriasCheckInList(null));

        var result = await CreateSut().GetCheckedInAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_UNAVAILABLE");
    }

    public static TheoryData<Exception> TransportFailures => new()
    {
        new HttpRequestException("caído"),
        new HttpRequestException("circuito abierto", new BrokenCircuitException()),
        new JsonException("cuerpo ilegible"),
    };

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public async Task GetCheckedInAsync_ReturnsAttendanceUnavailable_WhenTheCallFails(Exception failure)
    {
        _client.ListCheckInsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(failure);

        var result = await CreateSut().GetCheckedInAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe("ATTENDANCE_UNAVAILABLE");
    }

    private void Answer(params ConvocatoriasCheckIn[] rows) =>
        _client.ListCheckInsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ConvocatoriasCheckInList(rows));

    private static ConvocatoriasCheckIn Row(string email, string? origin, Guid? contactId = null) =>
        new(email, "María", "García", "Viajes del Caribe SRL", contactId, origin, CheckedInAt);
}
