using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Database;

/// <summary>
/// La escritura de la puerta contra el Postgres de verdad (spec <c>09</c> §3.2 y §4): la condición
/// viaja dentro del <c>UPDATE</c>, y <c>ck_registration_check_in_pair</c> tiene que aguantar el
/// sello y el borrado. Cada test siembra su propia inscripción, así que ninguno ve las de otro.
/// </summary>
public sealed class CheckInRepositoryTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private static readonly DateTimeOffset First = new(2026, 3, 12, 18, 42, 0, TimeSpan.Zero);

    private readonly ConvocatoriasApiDbFactory _factory;

    public CheckInRepositoryTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task FindByQrCodeAsync_ReturnsTheRegistrationAndItsEvent_WhenTheCodeExists()
    {
        var eventId = Guid.NewGuid();
        var (registrationId, qrCode) = await SeedRegistrationAsync(eventId);

        var target = await FindAsync(qrCode);

        target.HasValue.ShouldBeTrue();
        target.Value.RegistrationId.ShouldBe(registrationId);
        target.Value.EventId.ShouldBe(eventId);
    }

    [Fact]
    public async Task FindByQrCodeAsync_ReturnsNone_WhenNoRegistrationHasTheCode()
    {
        var target = await FindAsync(Guid.NewGuid().ToString("N"));

        target.HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsTheRegistrationAndItsEvent_WhenTheIdExists()
    {
        var eventId = Guid.NewGuid();
        var (registrationId, _) = await SeedRegistrationAsync(eventId);

        var target = await FindByIdAsync(registrationId);

        target.HasValue.ShouldBeTrue();
        target.Value.ShouldBe(new CheckInTarget(registrationId, eventId));
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsNone_WhenNoRegistrationHasTheId()
    {
        var target = await FindByIdAsync(Guid.NewGuid());

        target.HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public async Task StampAsync_ChangesTheRowAndReadsItBack_WhenTheRegistrationHasNoCheckIn()
    {
        var (registrationId, _) = await SeedRegistrationAsync(Guid.NewGuid());
        var staff = Guid.NewGuid();

        var stamp = await StampAsync(registrationId, First, staff);

        stamp.Changed.ShouldBeTrue();
        stamp.RegistrationId.ShouldBe(registrationId);
        stamp.FirstName.ShouldBe("Ana");
        stamp.LastName.ShouldBe("Gómez");
        stamp.Company.ShouldBe("Acme");
        stamp.Reference.ShouldStartWith("REF-2026-");
        stamp.CheckedInAt.ShouldBe(First);
        stamp.CheckedInById.ShouldBe(staff);
        (await ReadStampAsync(registrationId)).ShouldBe((First, staff, First));
    }

    /// <summary>El segundo escaneo no pisa al primero: la hora y quién son los de la primera vez.</summary>
    [Fact]
    public async Task StampAsync_ChangesNothingAndKeepsTheFirstStamp_WhenTheRegistrationIsAlreadyCheckedIn()
    {
        var (registrationId, _) = await SeedRegistrationAsync(Guid.NewGuid());
        var firstStaff = Guid.NewGuid();
        await StampAsync(registrationId, First, firstStaff);

        var stamp = await StampAsync(registrationId, First.AddMinutes(3), Guid.NewGuid());

        stamp.Changed.ShouldBeFalse();
        stamp.CheckedInAt.ShouldBe(First);
        stamp.CheckedInById.ShouldBe(firstStaff);
        (await ReadStampAsync(registrationId)).ShouldBe((First, firstStaff, First));
    }

    [Fact]
    public async Task ClearAsync_NullsBothColumnsAndReturnsWhatItCleared_WhenTheRegistrationIsCheckedIn()
    {
        var eventId = Guid.NewGuid();
        var (registrationId, _) = await SeedRegistrationAsync(eventId);
        var staff = Guid.NewGuid();
        await StampAsync(registrationId, First, staff);
        var undoneAt = First.AddMinutes(10);

        var clearance = await ClearAsync(eventId, registrationId, undoneAt);

        clearance.HasValue.ShouldBeTrue();
        clearance.Value.Cleared.ShouldNotBeNull();
        clearance.Value.Cleared.CheckedInAt.ShouldBe(First);
        clearance.Value.Cleared.CheckedInById.ShouldBe(staff);
        (await ReadStampAsync(registrationId)).ShouldBe((null, null, undoneAt));
    }

    [Fact]
    public async Task ClearAsync_ReturnsNothingClearedAndLeavesTheRow_WhenTheRegistrationHasNoCheckIn()
    {
        var eventId = Guid.NewGuid();
        var (registrationId, _) = await SeedRegistrationAsync(eventId);
        var before = await ReadStampAsync(registrationId);

        var clearance = await ClearAsync(eventId, registrationId, First);

        clearance.HasValue.ShouldBeTrue();
        clearance.Value.Cleared.ShouldBeNull();
        (await ReadStampAsync(registrationId)).ShouldBe(before);
    }

    /// <summary>Otro evento es lo mismo que no existir, y su check-in no se toca.</summary>
    [Fact]
    public async Task ClearAsync_ReturnsNoneAndKeepsTheCheckIn_WhenTheRegistrationIsOnAnotherEvent()
    {
        var (registrationId, _) = await SeedRegistrationAsync(Guid.NewGuid());
        var staff = Guid.NewGuid();
        await StampAsync(registrationId, First, staff);

        var clearance = await ClearAsync(Guid.NewGuid(), registrationId, First.AddMinutes(10));

        clearance.HasNoValue.ShouldBeTrue();
        (await ReadStampAsync(registrationId)).ShouldBe((First, staff, First));
    }

    [Fact]
    public async Task ClearAsync_ReturnsNone_WhenTheRegistrationDoesNotExist()
    {
        var clearance = await ClearAsync(Guid.NewGuid(), Guid.NewGuid(), First);

        clearance.HasNoValue.ShouldBeTrue();
    }

    /// <summary>Deshecho, el mismo código vuelve a entrar con un sello nuevo.</summary>
    [Fact]
    public async Task StampAsync_ChangesTheRowAgain_AfterTheCheckInWasCleared()
    {
        var eventId = Guid.NewGuid();
        var (registrationId, _) = await SeedRegistrationAsync(eventId);
        await StampAsync(registrationId, First, Guid.NewGuid());
        await ClearAsync(eventId, registrationId, First.AddMinutes(1));
        var again = First.AddMinutes(2);
        var staff = Guid.NewGuid();

        var stamp = await StampAsync(registrationId, again, staff);

        stamp.Changed.ShouldBeTrue();
        stamp.CheckedInAt.ShouldBe(again);
        stamp.CheckedInById.ShouldBe(staff);
    }

    private async Task<Maybe<CheckInTarget>> FindAsync(string qrCode)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var result = await Repository(scope).FindByQrCodeAsync(qrCode, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task<Maybe<CheckInTarget>> FindByIdAsync(Guid registrationId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var result = await Repository(scope).FindByIdAsync(registrationId, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task<CheckInStamp> StampAsync(
        Guid registrationId,
        DateTimeOffset now,
        Guid checkedInById)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var result = await Repository(scope).StampAsync(registrationId, now, checkedInById, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private async Task<Maybe<CheckInClearance>> ClearAsync(
        Guid eventId,
        Guid registrationId,
        DateTimeOffset now)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var result = await Repository(scope).ClearAsync(eventId, registrationId, now, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static ICheckInRepository Repository(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ICheckInRepository>();

    /// <summary>
    /// Las dos columnas del check-in y <c>updated_at</c>, leídas por SQL. Que la fila se lea ya
    /// prueba <c>ck_registration_check_in_pair</c>: un par a medias no se habría podido escribir.
    /// </summary>
    private async Task<(DateTimeOffset? CheckedInAt, Guid? CheckedInById, DateTimeOffset UpdatedAt)> ReadStampAsync(
        Guid registrationId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        var row = await context.Database.SqlQuery<StampRow>($"""
                SELECT checked_in_at AS "CheckedInAt", checked_in_by_id AS "CheckedInById", updated_at AS "UpdatedAt"
                FROM convocatorias.registration WHERE id = {registrationId}
                """)
            .SingleAsync();

        (row.CheckedInAt is null).ShouldBe(row.CheckedInById is null);
        return (row.CheckedInAt, row.CheckedInById, row.UpdatedAt);
    }

    /// <summary>Una inscripción sin check-in, con <c>updated_at</c> en <see cref="First"/>.</summary>
    private async Task<(Guid RegistrationId, string QrCode)> SeedRegistrationAsync(Guid eventId)
    {
        var id = Guid.CreateVersion7();
        var unique = Guid.NewGuid().ToString("N");
        var email = $"{unique}@acme.com";
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, first_name, last_name, email, email_normalized, company, reference, qr_code,
                 contact_details_differ, created_at, updated_at)
            VALUES ({id}, {eventId}, 'Ana', 'Gómez', {email}, {email}, 'Acme',
                    {"MITUR-2026-" + unique}, {unique}, false, {First}, {First})
            """);
        return (id, unique);
    }

    private sealed class StampRow
    {
        public DateTimeOffset? CheckedInAt { get; init; }

        public Guid? CheckedInById { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }
}
