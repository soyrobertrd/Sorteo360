using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.EventEndpoints;

/// <summary>
/// Simula la carrera cerrar-vs-cancelar sobre el mismo evento: dos escrituras
/// que leyeron la misma versión de la fila. El token de concurrencia optimista
/// (columna de sistema xmin) hace que la segunda escritura falle y se traduzca
/// a conflicto (409) en lugar de pisar silenciosamente el estado del ganador.
/// </summary>
public sealed class EventConcurrencyTests : IClassFixture<EventsApiDbFactory>
{
    private readonly EventsApiDbFactory _factory;
    private readonly HttpClient _client;

    public EventConcurrencyTests(EventsApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CloseVsCancel_ReturnsConflictToTheLoserAndKeepsTheWinnersState_WhenBothRaceOnOneEvent()
    {
        var eventId = await CreateEventInProgressAsync();

        // El "cierre" carga la fila ANTES de que la cancelación escriba: misma
        // ventana load-then-write que tendrían dos requests concurrentes.
        await using var staleScope = _factory.Services.CreateAsyncScope();
        var staleContext = staleScope.ServiceProvider.GetRequiredService<EventsDbContext>();
        // Fuera de una petición no hay cuenta (R6, falla cerrado): se lee sin el filtro de cuenta.
        var staleRow = await staleContext.Events.IgnoreQueryFilters().SingleAsync(e => e.Id == eventId);

        // La cancelación gana la carrera a través del endpoint real.
        var cancel = await _client.PostAsJsonAsync($"v1/events/{eventId}/cancel",
            new CancelEventRequest { Reason = "Recorte presupuestario." });
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);

        // El cierre perdedor escribe sobre la versión vieja de la fila: el token
        // xmin ya no coincide y DbResult lo traduce a conflicto.
        staleRow.Status = EventStatus.Closed;
        staleRow.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await DbResult.TryAsync<bool>(NullLogger.Instance, "cerrar el evento", async () =>
        {
            await staleContext.SaveChangesAsync();
            return true;
        });

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CONCURRENCY_CONFLICT");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);

        // El estado persistido es el del ganador: el evento sigue cancelado.
        var get = await _client.GetAsync($"v1/events/{eventId}");
        var fetched = await get.Content.ReadFromJsonAsync<EventDetailResponse>();
        fetched.ShouldNotBeNull();
        fetched!.Status.ShouldBe(EventStatus.Cancelled);
    }

    #region Helpers

    private async Task<Guid> CreateEventInProgressAsync()
    {
        var request = new CreateEventRequest
        {
            Type        = EventType.Roadshow,
            Name      = "Roadshow Concurrencia",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate    = new DateOnly(2026, 9, 5),
            City      = "Santo Domingo",
            Country        = "Dominican Republic"
        };
        var response = await _client.PostAsJsonAsync("v1/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var eventId = await response.Content.ReadFromJsonAsync<Guid>();

        (await _client.PostAsync($"v1/events/{eventId}/start", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        return eventId;
    }

    #endregion
}
