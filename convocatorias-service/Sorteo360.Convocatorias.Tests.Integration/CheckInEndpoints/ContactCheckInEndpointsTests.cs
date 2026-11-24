using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CheckInEndpoints;

/// <summary>
/// Los eventos a los que asistió un contacto del CRM (PGTI-170). Cada test siembra sus inscripciones
/// por SQL con contactos recién inventados, así que ninguno ve las filas de otro.
/// <para>
/// El check-in se siembra con las dos columnas puestas, que es lo que dejará el escaneo de la
/// puerta (spec <c>09</c> §3): esta lectura no depende de cómo se llegó ahí.
/// </para>
/// </summary>
public sealed class ContactCheckInEndpointsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private const string Route = "/v1/convocatorias/check-ins";

    private readonly ConvocatoriasApiDbFactory _factory;

    public ContactCheckInEndpointsTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task ListContactCheckIns_Returns403_WhenTheCallerLacksConvocatoriasRead()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsWrite);

        var response = await client.GetAsync($"{Route}?contactId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListContactCheckIns_Returns400_WhenNoContactIdIsSent()
    {
        var response = await _factory.CreateClient().GetAsync(Route);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListContactCheckIns_Returns400_WhenMoreThanAHundredContactIdsAreSent()
    {
        var query = string.Join('&', Enumerable.Range(0, 101).Select(_ => $"contactId={Guid.NewGuid()}"));

        var response = await _factory.CreateClient().GetAsync($"{Route}?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Participar es haber entrado (CRM GLOSSARY.md, <i>Participation</i>): inscribirse y no venir no
    /// cuenta, y estar solo invitado ni siquiera deja una inscripción.
    /// </summary>
    [Fact]
    public async Task ListContactCheckIns_ReturnsOnlyTheEventItShowedUpAt_WhenTheContactAlsoRegisteredForAnotherWithoutComing()
    {
        var contact = Guid.NewGuid();
        var attended = Guid.NewGuid();
        var checkedInAt = new DateTimeOffset(2026, 3, 12, 18, 42, 0, TimeSpan.Zero);
        await SeedRegistrationAsync(attended, contact, checkedInAt);
        await SeedRegistrationAsync(Guid.NewGuid(), contact, checkedInAt: null);

        var row = (await ListAsync(contact)).ShouldHaveSingleItem();

        row.GetProperty("contactId").GetGuid().ShouldBe(contact);
        row.GetProperty("eventId").GetGuid().ShouldBe(attended);
        row.GetProperty("checkedInAt").GetDateTimeOffset().ShouldBe(checkedInAt);
    }

    /// <summary>
    /// Quien entró sin ser contacto del CRM no le cuenta a nadie, ni al contacto que le reenvió el
    /// enlace: la fila es del contacto propio de la inscripción (spec <c>09</c> §7.1).
    /// </summary>
    [Fact]
    public async Task ListContactCheckIns_ReturnsNothing_WhenOnlyAWalkInWithNoContactShowedUpAtTheEvent()
    {
        var contact = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await SeedRegistrationAsync(eventId, contact, checkedInAt: null);
        await SeedRegistrationAsync(eventId, contactId: null, DateTimeOffset.UtcNow);

        var rows = await ListAsync(contact);

        rows.ShouldBeEmpty();
    }

    /// <summary>
    /// La empresa (PGTI-171) pregunta por todos sus contactos de una vez. Cada fila dice de quién
    /// es, y un contacto que no se pidió no aparece aunque haya venido al mismo evento.
    /// </summary>
    [Fact]
    public async Task ListContactCheckIns_ReturnsTheRowsOfEveryAskedContactNewestFirst_WhenSeveralContactIdsAreSent()
    {
        var ana = Guid.NewGuid();
        var luis = Guid.NewGuid();
        var notAsked = Guid.NewGuid();
        var fitur = Guid.NewGuid();
        var roadshow = Guid.NewGuid();
        var january = new DateTimeOffset(2026, 1, 22, 10, 0, 0, TimeSpan.Zero);
        var may = new DateTimeOffset(2026, 5, 6, 15, 30, 0, TimeSpan.Zero);
        await SeedRegistrationAsync(fitur, ana, january);
        await SeedRegistrationAsync(roadshow, ana, may);
        await SeedRegistrationAsync(fitur, luis, january.AddMinutes(5));
        await SeedRegistrationAsync(fitur, notAsked, january);

        var rows = await ListAsync(ana, luis);

        rows.Select(r => (r.GetProperty("contactId").GetGuid(), r.GetProperty("eventId").GetGuid()))
            .ShouldBe([(ana, roadshow), (luis, fitur), (ana, fitur)]);
    }

    /// <summary>
    /// Un mismo contacto puede entrar con dos inscripciones del mismo evento si se inscribió con dos
    /// direcciones. Es un evento al que asistió, no dos, y la hora es la de la primera entrada.
    /// </summary>
    [Fact]
    public async Task ListContactCheckIns_ReturnsOneRowWithTheEarliestTime_WhenTheContactCheckedInTwiceAtOneEvent()
    {
        var contact = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var first = new DateTimeOffset(2026, 4, 2, 9, 0, 0, TimeSpan.Zero);
        await SeedRegistrationAsync(eventId, contact, first.AddHours(2));
        await SeedRegistrationAsync(eventId, contact, first);

        var row = (await ListAsync(contact)).ShouldHaveSingleItem();

        row.GetProperty("checkedInAt").GetDateTimeOffset().ShouldBe(first);
    }

    private async Task<JsonElement[]> ListAsync(params Guid[] contactIds)
    {
        var query = string.Join('&', contactIds.Select(id => $"contactId={id}"));
        var response = await _factory.CreateClient().GetAsync($"{Route}?{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return [.. JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.EnumerateArray()];
    }

    /// <summary>Una inscripción ya sincronizada con el CRM, con o sin check-in.</summary>
    private async Task SeedRegistrationAsync(Guid eventId, Guid? contactId, DateTimeOffset? checkedInAt)
    {
        var unique = Guid.NewGuid().ToString("N");
        var email = $"{unique}@acme.com";
        Guid? checkedInById = checkedInAt is null ? null : Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, first_name, last_name, email, email_normalized, reference, qr_code,
                 contact_details_differ, crm_contact_id, checked_in_at, checked_in_by_id, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, {eventId}, 'Ana', 'Gómez', {email}, {email},
                    {"MITUR-2026-" + unique}, {unique}, false, {contactId}, {checkedInAt}, {checkedInById},
                    now(), now())
            """);
    }
}
