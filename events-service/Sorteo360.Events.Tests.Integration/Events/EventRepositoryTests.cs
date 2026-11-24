using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.Events;

/// <summary>
/// Una edición que se leyó antes de que el evento cambiara de estado, contra un PostgreSQL real.
/// </summary>
/// <remarks>
/// El servicio lee el evento, el evento produce la edición y el repositorio la guarda después: en
/// ese tramo otra petición puede iniciarlo o cancelarlo.
/// </remarks>
public sealed class EventRepositoryTests : IClassFixture<EventsApiDbFactory>
{
    /// <summary>El instante va en segundos enteros, para que la precisión de <c>timestamptz</c> no lo cambie.</summary>
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EditedAt = new(2026, 11, 15, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly EventsApiDbFactory _factory;

    public EventRepositoryTests(EventsApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotEditable_WhenTheEventWasCancelledAfterItWasRead()
    {
        var staleEdit = await SeedAndEditAsync();

        (await InOwnScopeAsync(r => r.CancelAsync(staleEdit.Id, "Suspendido por lluvia", UserId, CancellationToken.None)))
            .IsSuccess.ShouldBeTrue();

        var result = await InOwnScopeAsync(r => r.UpdateAsync(staleEdit, CancellationToken.None));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EVENT_NOT_EDITABLE");

        var stored = await ReadFreshAsync(staleEdit.Id);
        stored.Status.ShouldBe(EventStatus.Cancelled);
        stored.Name.ShouldBe("Roadshow original");
        stored.EstimatedBudget.ShouldBe(new Money(5000m, Currency.USD));
        stored.CancellationReason.ShouldBe("Suspendido por lluvia");
    }

    [Fact]
    public async Task UpdateAsync_SavesTheEdit_WhenTheEventWasStartedAfterItWasRead()
    {
        var staleEdit = await SeedAndEditAsync();

        (await InOwnScopeAsync(r => r.StartAsync(staleEdit.Id, UserId, CancellationToken.None)))
            .IsSuccess.ShouldBeTrue();

        var result = await InOwnScopeAsync(r => r.UpdateAsync(staleEdit, CancellationToken.None));

        result.IsSuccess.ShouldBeTrue();

        var stored = await ReadFreshAsync(staleEdit.Id);
        stored.Name.ShouldBe("Roadshow editado");
        stored.Status.ShouldBe(EventStatus.InProgress);
    }

    /// <summary>
    /// Crea un evento en <c>planning</c>, lo lee y devuelve, sin guardar, la edición que el evento
    /// produce, como haría el servicio antes de guardar.
    /// </summary>
    private async Task<Event> SeedAndEditAsync()
    {
        var eventId = Guid.NewGuid();
        var @event = new Event(
            id: eventId,
            type: EventType.Roadshow,
            name: "Roadshow original",
            startDate: new DateOnly(2026, 12, 1),
            endDate: new DateOnly(2026, 12, 5),
            status: EventStatus.Planning,
            language: AllowedLanguage.Espanol,
            visibility: EventVisibility.Private,
            seatingLayout: null,
            externalUrl: null,
            description: null,
            invitedCount: null,
            estimatedBudget: new Money(5000m, Currency.USD),
            totalCost: null,
            cancellationReason: null,
            createdAt: CreatedAt,
            updatedAt: CreatedAt,
            createdById: UserId,
            updatedById: UserId,
            deletedAt: null);
        var city = new RoadshowCity(
            id: Guid.NewGuid(),
            eventId: eventId,
            name: "Madrid",
            country: "España",
            timeZone: "Europe/Madrid",
            date: null,
            displayOrder: 1,
            createdAt: CreatedAt,
            updatedAt: CreatedAt,
            createdById: UserId,
            updatedById: UserId,
            deletedAt: null);

        (await InOwnScopeAsync(r => r.CreateAsync(@event, city, CancellationToken.None))).IsSuccess.ShouldBeTrue();

        var loaded = (await InOwnScopeAsync(r => r.GetByIdAsync(eventId, CancellationToken.None))).Value.Value.Event;
        var dto = new UpdateEventDto(
            Id: eventId,
            Name: "Roadshow editado",
            StartDate: loaded.StartDate,
            EndDate: loaded.EndDate,
            Language: loaded.Language,
            Visibility: null,
            SeatingLayout: null,
            ExternalUrl: null,
            Description: null,
            InvitedCount: null,
            EstimatedBudget: new Money(9000m, Currency.USD),
            UpdatedById: UserId);

        return loaded.Update(dto, EditedAt).Value;
    }

    /// <summary>Lee el evento tal como quedó en la base.</summary>
    private async Task<Event> ReadFreshAsync(Guid eventId)
        => (await InOwnScopeAsync(r => r.GetByIdAsync(eventId, CancellationToken.None))).Value.Value.Event;

    /// <summary>Corre un paso con un repositorio y un contexto propios, como lo haría una petición aparte.</summary>
    /// <remarks>
    /// Un contexto compartido entre pasos devolvería en la segunda lectura del repositorio la fila
    /// que ya rastrea, con el estado y el xmin viejos, y no la de la base. Eso no pasa en producción,
    /// donde cada petición tiene su contexto.
    /// </remarks>
    private async Task<T> InOwnScopeAsync<T>(Func<EventRepository, Task<T>> step)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        // Fuera de una petición no hay claim account_id y el filtro de cuenta esconde todo (R6):
        // el contexto se arma con la cuenta por omisión, como la tendría una petición autenticada.
        await using var context = new EventsDbContext(
            scope.ServiceProvider.GetRequiredService<DbContextOptions<EventsDbContext>>(), new DefaultAccount());
        return await step(new EventRepository(context, NullLogger<EventRepository>.Instance));
    }

    private sealed class DefaultAccount : ICurrentAccount
    {
        public Guid AccountId => Account.DefaultBanreservasAccountId;
    }
}
