using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Platform.Errors;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Events;

public sealed class EventTests
{
    private static readonly DateTimeOffset LoadedAt = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid EventId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid CreatorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid EditorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public void Update_ReturnsConflict_WhenEventIsTerminal(string status)
    {
        var result = Loaded(status).Update(FullDto(), Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_ReturnsInvalid_WhenBudgetAmountIsNotPositive(decimal amount)
    {
        var result = Loaded().Update(FullDto(estimatedBudget: new Money(amount, Currency.USD)), Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_BUDGET_AMOUNT_INVALID");
        result.Error.Kind.ShouldBe(ErrorKind.Invalid);
    }

    // El estado se revisa antes que el presupuesto: a un evento terminal no se le explica qué
    // monto admitiría, porque no admite ninguno.
    [Fact]
    public void Update_ReturnsNotEditable_WhenTerminalAndBudgetIsInvalid()
    {
        var result = Loaded(EventStatus.Cancelled)
            .Update(FullDto(estimatedBudget: new Money(0m, Currency.USD)), Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");
    }

    [Fact]
    public void Update_Throws_WhenTheDtoNamesAnotherEvent()
    {
        Should.Throw<ArgumentException>(() => Loaded().Update(FullDto() with { Id = Guid.NewGuid() }, Now));
    }

    [Fact]
    public void Update_Throws_WhenTheDtoHasNoName()
    {
        Should.Throw<ArgumentNullException>(() => Loaded().Update(FullDto() with { Name = null! }, Now));
    }

    [Fact]
    public void Update_Throws_WhenTheDtoHasNoLanguage()
    {
        Should.Throw<ArgumentNullException>(() => Loaded().Update(FullDto() with { Language = null! }, Now));
    }

    [Fact]
    public void Update_ReplacesEveryEditableField()
    {
        var updated = Loaded().Update(FullDto(), Now).Value;

        updated.Name.ShouldBe("Roadshow editado");
        updated.StartDate.ShouldBe(new DateOnly(2026, 10, 1));
        updated.EndDate.ShouldBe(new DateOnly(2026, 10, 7));
        updated.Language.ShouldBe("en");
        updated.Visibility.ShouldBe("private");
        updated.SeatingLayout.ShouldBe("Classroom");
        updated.ExternalUrl.ShouldBe("https://example.org/editado");
        updated.Description.ShouldBe("Descripción editada");
        updated.InvitedCount.ShouldBe(80);
        updated.EstimatedBudget.ShouldBe(new Money(7000m, Currency.EUR));
        updated.UpdatedAt.ShouldBe(Now);
        updated.UpdatedById.ShouldBe(EditorId);
    }

    [Fact]
    public void Update_KeepsVisibility_WhenTheDtoOmitsIt()
    {
        var updated = Loaded().Update(FullDto(visibility: null), Now).Value;

        updated.Visibility.ShouldBe("public");
    }

    [Fact]
    public void Update_ReplacesVisibility_WhenTheDtoStatesIt()
    {
        var updated = Loaded().Update(FullDto(visibility: EventVisibility.Private), Now).Value;

        updated.Visibility.ShouldBe("private");
    }

    [Fact]
    public void Update_ClearsOptionalFields_WhenTheDtoOmitsThem()
    {
        var dto = FullDto() with
        {
            SeatingLayout = null,
            ExternalUrl = null,
            Description = null,
            InvitedCount = null,
            EstimatedBudget = null,
        };

        var updated = Loaded().Update(dto, Now).Value;

        updated.SeatingLayout.ShouldBeNull();
        updated.ExternalUrl.ShouldBeNull();
        updated.Description.ShouldBeNull();
        updated.InvitedCount.ShouldBeNull();
        updated.EstimatedBudget.ShouldBeNull();
    }

    [Theory]
    [InlineData(EventStatus.Planning)]
    [InlineData(EventStatus.InProgress)]
    public void Update_KeepsTheFieldsAnEditDoesNotOwn(string status)
    {
        var updated = Loaded(status).Update(FullDto(), Now).Value;

        updated.Id.ShouldBe(EventId);
        updated.Type.ShouldBe(EventType.Corporate);
        updated.Status.ShouldBe(status);
        updated.TotalCost.ShouldBe(new Money(4800m, Currency.USD));
        updated.CancellationReason.ShouldBe("Motivo cargado");
        updated.CreatedAt.ShouldBe(LoadedAt);
        updated.CreatedById.ShouldBe(CreatorId);
        updated.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void EventType_ExposesGenericEventTypesWithoutLegacyTravelValues()
    {
        EventType.All.ShouldBe(
            ["Corporate", "Christmas", "Wedding", "Birthday", "Concert", "Graduation", "Conference", "Other"],
            ignoreOrder: true);
    }

    // El evento tal como lo devolvería la base: todos los campos llenos, para que un campo que la
    // edición no toca se note si cambia.
    private static Event Loaded(string status = EventStatus.Planning) => new(
        id: EventId,
        type: EventType.Roadshow,
        name: "Roadshow original",
        startDate: new DateOnly(2026, 9, 1),
        endDate: new DateOnly(2026, 9, 5),
        status: status,
        language: AllowedLanguage.Espanol,
        visibility: EventVisibility.Public,
        seatingLayout: SeatingLayout.Banquet,
        externalUrl: "https://example.org/original",
        description: "Descripción original",
        invitedCount: 120,
        estimatedBudget: new Money(5000m, Currency.USD),
        totalCost: new Money(4800m, Currency.USD),
        cancellationReason: "Motivo cargado",
        createdAt: LoadedAt,
        updatedAt: LoadedAt,
        createdById: CreatorId,
        updatedById: CreatorId,
        deletedAt: null);

    // Una edición que cambia todos los campos editables respecto de Loaded.
    private static UpdateEventDto FullDto(
        string? visibility = EventVisibility.Private,
        Money? estimatedBudget = null) => new(
        Id: EventId,
        Name: "Roadshow editado",
        StartDate: new DateOnly(2026, 10, 1),
        EndDate: new DateOnly(2026, 10, 7),
        Language: AllowedLanguage.Ingles,
        Visibility: visibility,
        SeatingLayout: SeatingLayout.Classroom,
        ExternalUrl: "https://example.org/editado",
        Description: "Descripción editada",
        InvitedCount: 80,
        EstimatedBudget: estimatedBudget ?? new Money(7000m, Currency.EUR),
        UpdatedById: EditorId);
}
