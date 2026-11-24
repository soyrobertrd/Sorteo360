using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.CheckIns;

/// <summary>
/// La lista de acceso de un evento (spec <c>09</c> §5.1): los totales del evento entero, el filtro,
/// el orden y la paginación, sobre las filas que lee el repositorio.
/// </summary>
public sealed class DoorListServiceTests
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 18, 42, 0, TimeSpan.Zero);

    private readonly IDoorListRepository _repository = Substitute.For<IDoorListRepository>();
    private readonly DoorListService _service;

    public DoorListServiceTests()
    {
        _service = new DoorListService(_repository);
    }

    [Fact]
    public async Task ListAsync_ReturnsTheEventTotals_WhateverTheSearchAndCheckedInFilter()
    {
        GivenRows(
            Row("María", "García", "00001", checkedIn: true),
            Row("Luis", "Peña", "00002", checkedIn: true),
            Row("Ana", "Gómez", "00003"));

        var result = await _service.ListAsync(EventId, "zzz", checkedIn: false, 1, 50, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Totals.ShouldBe(new DoorListTotals(Registered: 3, CheckedIn: 2));
        result.Value.Page.Items.ShouldBeEmpty();
        result.Value.Page.Total.ShouldBe(0);
    }

    [Theory]
    [InlineData(true, "00001")]
    [InlineData(false, "00002")]
    public async Task ListAsync_ReturnsOnlyTheRowsWithThatCheckInState_WhenCheckedInIsGiven(bool checkedIn, string expected)
    {
        GivenRows(Row("María", "García", "00001", checkedIn: true), Row("Luis", "Peña", "00002"));

        var result = await _service.ListAsync(EventId, null, checkedIn, 1, 50, CancellationToken.None);

        result.Value.Page.Items.ShouldHaveSingleItem().Reference.ShouldBe(expected);
    }

    [Fact]
    public async Task ListAsync_ReturnsTheRowsByLastNameFirstNameAndReference_WhenNamesDifferOnlyByCaseOrAccents()
    {
        GivenRows(
            Row("Zoe", "Pérez", "00005"),
            Row("Ana", "Pérez", "00004"),
            Row("Ana", "perez", "00003"),
            Row("Luis", "Álvarez", "00002"),
            Row("Juan", "Zapata", "00001"));

        var result = await _service.ListAsync(EventId, null, null, 1, 50, CancellationToken.None);

        result.Value.Page.Items.Select(row => row.Reference)
            .ShouldBe(["00002", "00003", "00004", "00005", "00001"]);
    }

    [Fact]
    public async Task ListAsync_ReturnsTheRequestedPageAndTheFilteredTotal_WhenPaged()
    {
        GivenRows(Row("A", "A", "00001"), Row("B", "B", "00002"), Row("C", "C", "00003"));

        var result = await _service.ListAsync(EventId, null, null, page: 2, pageSize: 2, CancellationToken.None);

        result.Value.Page.Items.ShouldHaveSingleItem().Reference.ShouldBe("00003");
        result.Value.Page.Total.ShouldBe(3);
        result.Value.Page.Page.ShouldBe(2);
        result.Value.Page.PageSize.ShouldBe(2);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-3, 500, 1, 100)]
    public async Task ListAsync_ReturnsClampedPaging_WhenPageOrPageSizeIsOutOfRange(
        int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        GivenRows();

        var result = await _service.ListAsync(EventId, null, null, page, pageSize, CancellationToken.None);

        result.Value.Page.Page.ShouldBe(expectedPage);
        result.Value.Page.PageSize.ShouldBe(expectedPageSize);
    }

    [Fact]
    public async Task ListAsync_ReturnsTheRepositoryError_WhenTheReadFails()
    {
        // Cualquier error sirve: lo que se prueba es que pasa sin tocar.
        var error = new CheckInCodeNotFoundError();
        _repository.ReadAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<DoorListRow>, BaseError>(error));

        var result = await _service.ListAsync(EventId, null, null, 1, 50, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    private void GivenRows(params DoorListRow[] rows) =>
        _repository.ReadAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<DoorListRow>, BaseError>(rows));

    private static DoorListRow Row(string firstName, string lastName, string reference, bool checkedIn = false) => new(
        Guid.NewGuid(), firstName, lastName, null, reference, $"{reference}@acme.com", checkedIn ? Now : null);
}
