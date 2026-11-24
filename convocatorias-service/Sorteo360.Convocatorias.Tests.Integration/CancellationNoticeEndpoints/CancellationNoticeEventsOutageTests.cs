using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CancellationNoticeEndpoints;

/// <summary>
/// events-service caído cuando llega el aviso de cancelación (spec <c>06</c> §4.7): sin leer el
/// evento no se sabe si de verdad está cancelado, así que no se le escribe a nadie.
/// <para>
/// Va en su propia clase porque el cortacircuitos del chasis es un singleton: abierto acá, tumbaría
/// la clase vecina por una caída que no es suya.
/// </para>
/// </summary>
public sealed class CancellationNoticeEventsOutageTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public CancellationNoticeEventsOutageTests(RegistrationPortalFactory factory) => _factory = factory;

    [Fact]
    public async Task NotifyCancellation_Returns503AndMailsNobody_WhenEventsServiceIsDown()
    {
        var eventId = Guid.NewGuid();
        _factory.Events.ServeEvents(new StubEvent(
            eventId, "Roadshow Madrid 2026", EventsStub.PublicVisibility, "es", StubEventStatus.InProgress));
        var email = EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");
        (await RegistrationTestClient.RegisterAsync(_factory.CreateClient(), eventId, email: email))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        _factory.Events.GoDown();

        var response = await _factory.CreateClient()
            .PostAsync($"/v1/convocatorias/events/{eventId}/cancellation-notice", null);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().ShouldBe(EventsUnavailableError.ErrorCode);
        (await NoticesAndStampsAsync(eventId, email)).ShouldBe(0);
    }

    /// <summary>Avisos mandados a esa dirección más filas selladas del evento: los dos tienen que ser cero.</summary>
    private async Task<int> NoticesAndStampsAsync(Guid eventId, string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<int>($"""
                SELECT ((SELECT COUNT(*) FROM convocatorias.stub_notification
                         WHERE recipient_email = {email}
                           AND type = {CancellationNoticeService.NotificationType})
                      + (SELECT COUNT(*) FROM convocatorias.registration
                         WHERE event_id = {eventId} AND cancellation_notified_at IS NOT NULL))::int AS "Value"
                """)
            .SingleAsync();
    }
}
