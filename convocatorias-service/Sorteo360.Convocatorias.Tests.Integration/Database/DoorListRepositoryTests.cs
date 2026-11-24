using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Database;

/// <summary>
/// La lectura de la lista de acceso contra el Postgres de verdad (spec <c>09</c> §5.1): todas las
/// inscripciones del evento y ninguna de otro, con la dirección normalizada y el check-in.
/// </summary>
public sealed class DoorListRepositoryTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 18, 42, 0, TimeSpan.Zero);

    private readonly ConvocatoriasApiDbFactory _factory;

    public DoorListRepositoryTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task ReadAsync_ReturnsOnlyThisEventsRegistrationsWithTheirCheckIn_WhenAnotherEventHasRegistrationsToo()
    {
        var eventId = Guid.NewGuid();
        var staff = Guid.NewGuid();
        var maria = await SeedRegistrationAsync(eventId, "María", "García", "Maria.Garcia@Viajes.com", checkedInById: staff);
        var luis = await SeedRegistrationAsync(eventId, "Luis", "Peña", "luis@acme.com", checkedInById: null);
        await SeedRegistrationAsync(Guid.NewGuid(), "Otro", "Evento", "otro@acme.com", checkedInById: null);

        await using var scope = _factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IDoorListRepository>()
            .ReadAsync(eventId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.OrderBy(row => row.FirstName).ShouldBe(
        [
            new DoorListRow(luis.Id, "Luis", "Peña", "Acme", luis.Reference, "luis@acme.com", null),
            new DoorListRow(maria.Id, "María", "García", "Acme", maria.Reference, "maria.garcia@viajes.com", Now),
        ]);
    }

    private async Task<(Guid Id, string Reference)> SeedRegistrationAsync(
        Guid eventId,
        string firstName,
        string lastName,
        string email,
        Guid? checkedInById)
    {
        var id = Guid.CreateVersion7();
        var unique = Guid.NewGuid().ToString("N");
        var reference = "MITUR-2026-" + unique;
        DateTimeOffset? checkedInAt = checkedInById is null ? null : Now;
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, first_name, last_name, email, email_normalized, company, reference, qr_code,
                 contact_details_differ, checked_in_at, checked_in_by_id, created_at, updated_at)
            VALUES ({id}, {eventId}, {firstName}, {lastName}, {email}, {email.Trim().ToLowerInvariant()}, 'Acme',
                    {reference}, {unique}, false, {checkedInAt}, {checkedInById}, {Now}, {Now})
            """);
        return (id, reference);
    }
}
