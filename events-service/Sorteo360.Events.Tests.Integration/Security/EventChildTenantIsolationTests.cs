using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Tests.Integration.RaffleEndpoints;

namespace Sorteo360.Events.Tests.Integration.Security;

/// <summary>
/// Auditoría run-1, pendientes 2, 4, 5 y 7: las tablas hijas del evento (premios, sorteos de premio,
/// imágenes y marca) no tienen cuenta propia y la heredan del evento. Cada prueba arma el dato en la
/// cuenta A (la de Banreservas, la que usa el cliente por omisión) y lo ataca desde la cuenta B.
/// </summary>
public sealed class EventChildTenantIsolationTests : IClassFixture<RaffleAttendanceApiDbFactory>
{
    private readonly RaffleAttendanceApiDbFactory _factory;
    private readonly RaffleTestApi _api;

    public EventChildTenantIsolationTests(RaffleAttendanceApiDbFactory factory)
    {
        _factory = factory;
        _api = new RaffleTestApi(factory, ClientFor(Account.DefaultBanreservasAccountId));
    }

    /// <summary>Pendiente 2: draw-batch y la foto de premios exigían el rol, no que el evento fuera de la cuenta.</summary>
    [Fact]
    public async Task DrawBatchAndPrizesImage_Return404AndWriteNothing_WhenTheEventIsOfAnotherAccount()
    {
        var (eventA, prizesA) = await _api.StartedRaffleAsync(_factory.Attendance, people: 2, prizes: 1);
        using var clientB = ClientFor(await SeedAccountAsync("Cuenta B sorteo"));

        var batch = await clientB.PostAsJsonAsync(
            $"v1/events/{eventA}/raffle/prizes/draw-batch", new { prizeIds = prizesA });
        var removeImage = await clientB.PostAsJsonAsync(
            $"v1/events/{eventA}/raffle/prizes/image/remove", new { prizeIds = prizesA });

        batch.StatusCode.ShouldBe(HttpStatusCode.NotFound, await batch.Content.ReadAsStringAsync());
        removeImage.StatusCode.ShouldBe(HttpStatusCode.NotFound, await removeImage.Content.ReadAsStringAsync());
        (await _api.GetStoredDrawsAsync(eventA)).ShouldBeEmpty();
    }

    /// <summary>Pendiente 4: el código de retiro se buscaba en todas las cuentas.</summary>
    [Fact]
    public async Task FindByCode_Returns404_WhenTheDrawIsOfAnotherAccount()
    {
        var (eventA, _) = await _api.DrawnPrizeAsync(_factory.Attendance);
        var code = (await _api.GetStoredDrawsAsync(eventA)).ShouldHaveSingleItem().RedemptionCode;
        using var clientA = ClientFor(Account.DefaultBanreservasAccountId);
        using var clientB = ClientFor(await SeedAccountAsync("Cuenta B códigos"));

        var fromB = await clientB.GetAsync($"v1/prize-draws/by-code/{code}");
        var fromA = await clientA.GetAsync($"v1/prize-draws/by-code/{code}");

        await RaffleTestApi.ShouldBeProblemAsync(fromB, HttpStatusCode.NotFound, "REDEMPTION_CODE_NOT_FOUND");
        fromA.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Pendiente 5: borrar una imagen solo miraba que la imagen fuera del evento de la ruta.</summary>
    [Fact]
    public async Task DeleteEventImage_Returns404AndKeepsTheImage_WhenTheEventIsOfAnotherAccount()
    {
        var eventA = await _api.CreateEventAsync();
        var imageId = await SeedImageAsync(eventA);
        using var clientA = ClientFor(Account.DefaultBanreservasAccountId);
        using var clientB = ClientFor(await SeedAccountAsync("Cuenta B imágenes"));

        var fromB = await clientB.DeleteAsync($"v1/events/{eventA}/images/{imageId}");

        fromB.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await clientA.GetStringAsync($"v1/events/{eventA}/images")).ShouldContain(imageId.ToString());
    }

    /// <summary>Pendiente 7: otra cuenta ocupaba la clave "event:{id}" y el dueño quedaba con 409.</summary>
    [Fact]
    public async Task EventBranding_Returns404ToAnotherAccount_AndTheOwnerCanStillSaveIt()
    {
        var eventA = await _api.CreateEventAsync();
        using var clientA = ClientFor(Account.DefaultBanreservasAccountId);
        using var clientB = ClientFor(await SeedAccountAsync("Cuenta B marca"));

        var fromB = await clientB.PutAsJsonAsync(
            $"v1/events/{eventA}/raffle/branding", new { companyName = "Intrusa", logoUrl = (string?)null });
        var fromA = await clientA.PutAsJsonAsync(
            $"v1/events/{eventA}/raffle/branding", new { companyName = "Dueña", logoUrl = (string?)null });

        fromB.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        fromA.StatusCode.ShouldBe(HttpStatusCode.OK, await fromA.Content.ReadAsStringAsync());
        (await clientA.GetStringAsync($"v1/events/{eventA}/raffle/branding")).ShouldContain("Dueña");
    }

    private HttpClient ClientFor(Guid accountId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AccountIdHeader, accountId.ToString("D"));
        return client;
    }

    private async Task<Guid> SeedAccountAsync(string name)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        db.Accounts.Add(new AccountData
        {
            Id = id,
            Name = name,
            Plan = PlanCatalog.Pro,
            Status = AccountStatus.Active,
            PlanStartedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>Una imagen de galería de la cuenta A, escrita directo: lo que se prueba es el borrado.</summary>
    private async Task<Guid> SeedImageAsync(Guid eventId)
    {
        var fileId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        db.StoredFiles.Add(new StoredFileData
        {
            Id = fileId,
            AccountId = Account.DefaultBanreservasAccountId,
            Provider = "filesystem",
            StorageKey = $"test/{fileId:N}.png",
            FileName = "galeria.png",
            ContentType = "image/png",
            SizeBytes = 128,
            Checksum = "abc",
            CreatedAt = now,
            CreatedById = TestAuthHandler.DefaultUserId,
        });
        db.EventImages.Add(new EventImageData
        {
            Id = imageId,
            EventId = eventId,
            StoredFileId = fileId,
            Kind = EventImageKind.Gallery,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedById = TestAuthHandler.DefaultUserId,
            UpdatedById = TestAuthHandler.DefaultUserId,
        });
        await db.SaveChangesAsync();
        return imageId;
    }
}
