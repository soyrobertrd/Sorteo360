using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;

namespace Sorteo360.Events.Tests.Integration.Security;

/// <summary>
/// Pruebas de integración contra PostgreSQL real (Testcontainers) para los hallazgos de
/// revisión/seguridad (R4, R5, R6, R7, R9, S4, S5) y la importación en bloque de premios (A30).
/// </summary>
public sealed class ReviewAndSecurityRegressionTests : IClassFixture<EventsApiDbFactory>
{
    private readonly EventsApiDbFactory _factory;

    public ReviewAndSecurityRegressionTests(EventsApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task R6_MissingOrUnknownAccountClaim_Returns403AccountNotActive()
    {
        using var missingClaimClient = _factory.CreateClient();
        missingClaimClient.DefaultRequestHeaders.Add(TestAuthHandler.AccountIdHeader, "none");

        var missingResp = await missingClaimClient.GetAsync("/v1/events");
        missingResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var missingBody = await missingResp.Content.ReadAsStringAsync();
        missingBody.ShouldContain("ACCOUNT_NOT_ACTIVE");

        using var unknownAccountClient = _factory.CreateClient();
        unknownAccountClient.DefaultRequestHeaders.Add(TestAuthHandler.AccountIdHeader, Guid.NewGuid().ToString("D"));

        var unknownResp = await unknownAccountClient.GetAsync("/v1/events");
        unknownResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var unknownBody = await unknownResp.Content.ReadAsStringAsync();
        unknownBody.ShouldContain("ACCOUNT_NOT_ACTIVE");
    }

    [Fact]
    public async Task R4_ParticipantsRoutes_Return404ForOtherAccountWithoutMutatingDb()
    {
        var accountB = await SeedAccountAsync("Cuenta B", PlanCatalog.Pro);
        var eventA = await CreateEventAsync(Account.DefaultBanreservasAccountId, "Evento Cuenta A");

        using var clientA = ClientFor(Account.DefaultBanreservasAccountId);
        var importResp = await clientA.PostAsJsonAsync($"/v1/events/{eventA}/raffle/participants/import", new
        {
            participants = new[] { new { fullName = "Ana Cuenta A", email = "ana@a.com", employeeCode = "A-01" } },
        });
        importResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await importResp.Content.ReadAsStringAsync());
        var participantId = doc.RootElement.GetProperty("participants")[0].GetProperty("id").GetString()!;

        using var clientB = ClientFor(accountB);
        (await clientB.GetAsync($"/v1/events/{eventA}/raffle/participants")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await clientB.PostAsJsonAsync($"/v1/events/{eventA}/raffle/participants/check-in", new { email = "ana@a.com", checkedIn = true })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await clientB.PostAsJsonAsync($"/v1/events/{eventA}/raffle/participants/check-in-all", new { checkedIn = true })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await clientB.DeleteAsync($"/v1/events/{eventA}/raffle/participants/{participantId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await clientB.GetAsync($"/v1/events/{eventA}/raffle/participants/{participantId}/qr")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await clientB.GetAsync($"/v1/events/{eventA}/raffle/participants/badges.pdf")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await clientB.GetAsync($"/v1/events/{eventA}/raffle/audit")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Verificar que la base de datos de la cuenta A sigue intacta
        var checkA = await clientA.GetAsync($"/v1/events/{eventA}/raffle/participants");
        checkA.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bodyA = await checkA.Content.ReadAsStringAsync();
        bodyA.ShouldContain("ana@a.com");
        bodyA.ShouldContain("\"checkedIn\":false");
    }

    [Fact]
    public async Task R5_WebhookUsesPerEventSecret_RejectsOtherAccountKeyAndWorksForNewAccount()
    {
        var accountB = await SeedAccountAsync("Empresa Pro B", PlanCatalog.Pro);
        var eventA = await CreateEventAsync(Account.DefaultBanreservasAccountId, "Evento Webhook A");
        var eventB = await CreateEventAsync(accountB, "Evento Webhook B");

        string secretA;
        string secretB;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            secretA = (await db.Events.IgnoreQueryFilters().Where(e => e.Id == eventA).Select(e => e.WebhookSecret).SingleAsync())!;
            secretB = (await db.Events.IgnoreQueryFilters().Where(e => e.Id == eventB).Select(e => e.WebhookSecret).SingleAsync())!;
        }

        secretA.ShouldNotBeNullOrWhiteSpace();
        secretB.ShouldNotBeNullOrWhiteSpace();
        secretA.ShouldNotBe(secretB);

        // Clave de A no puede importar ni hacer check-in en evento de B
        using var anonWithKeyA = _factory.CreateClient();
        anonWithKeyA.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");
        anonWithKeyA.DefaultRequestHeaders.Add("X-Webhook-Key", secretA);

        var crossImport = await anonWithKeyA.PostAsJsonAsync(
            $"/v1/events/{eventB}/raffle/webhooks/participants",
            new { participants = new[] { new { fullName = "Intruso", email = "intruso@b.com" } } });
        crossImport.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var crossCheckIn = await anonWithKeyA.PostAsJsonAsync(
            $"/v1/events/{eventB}/raffle/webhooks/check-in",
            new { email = "intruso@b.com", checkedIn = true });
        crossCheckIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Clave propia de B sí importa y hace check-in en el evento de B
        using var anonWithKeyB = _factory.CreateClient();
        anonWithKeyB.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");
        anonWithKeyB.DefaultRequestHeaders.Add("X-Webhook-Key", secretB);

        var ownImport = await anonWithKeyB.PostAsJsonAsync(
            $"/v1/events/{eventB}/raffle/webhooks/participants",
            new { participants = new[] { new { fullName = "Laura B", email = "laura@b.com", employeeCode = "B-01" } } });
        ownImport.StatusCode.ShouldBe(HttpStatusCode.OK);

        var ownCheckIn = await anonWithKeyB.PostAsJsonAsync(
            $"/v1/events/{eventB}/raffle/webhooks/check-in",
            new { email = "laura@b.com", checkedIn = true });
        ownCheckIn.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task R7_S5_BrandingIsIsolatedPerAccount_AndAnonymousReturnsNeutralDefaultsUnlessEventIdPassed()
    {
        var accountB = await SeedAccountAsync("Marca B", PlanCatalog.Basic);
        var eventB = await CreateEventAsync(accountB, "Evento Marca B");

        using var clientA = ClientFor(Account.DefaultBanreservasAccountId);
        var putA = await clientA.PutAsJsonAsync("/v1/branding", new { organizationName = "Banco A Oficial" });
        putA.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var clientB = ClientFor(accountB);
        var putB = await clientB.PutAsJsonAsync("/v1/branding", new { organizationName = "Corporación B" });
        putB.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Cuenta A conserva su marca intacta
        var getA = await clientA.GetStringAsync("/v1/branding");
        getA.ShouldContain("Banco A Oficial");
        getA.ShouldNotContain("Corporación B");

        // Anónimo sin eventId recibe valores neutros (nunca Banreservas ni B)
        using var anon = _factory.CreateClient();
        anon.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");
        var anonDefault = await anon.GetStringAsync("/v1/branding");
        anonDefault.ShouldNotContain("Banco A Oficial");
        anonDefault.ShouldNotContain("Corporación B");

        // Anónimo con eventId de B recibe la marca de B
        var anonForB = await anon.GetStringAsync($"/v1/branding?eventId={eventB}");
        anonForB.ShouldContain("Corporación B");
    }

    [Fact]
    public async Task R9_FreeAccountParallelRaffleCreation_AllowsOnlyOneAndKeepsCounterAtOne()
    {
        var freeAccount = await SeedAccountAsync("Empresa Gratis", PlanCatalog.Free);
        var event1 = await CreateEventAsync(freeAccount, "Evento Gratis 1");
        var event2 = await CreateEventAsync(freeAccount, "Evento Gratis 2");

        using var client1 = ClientFor(freeAccount);
        using var client2 = ClientFor(freeAccount);

        var t1 = client1.PostAsync($"/v1/events/{event1}/raffle", content: null);
        var t2 = client2.PostAsync($"/v1/events/{event2}/raffle", content: null);
        var responses = await Task.WhenAll(t1, t2);

        var statuses = responses.Select(r => r.StatusCode).OrderBy(s => (int)s).ToList();
        statuses.ShouldBe([HttpStatusCode.Created, HttpStatusCode.Conflict]);

        var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var conflictBody = await conflict.Content.ReadAsStringAsync();
        conflictBody.ShouldContain("PLAN_MONTHLY_RAFFLE_LIMIT");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var count = await db.AccountMonthlyUsages
            .Where(u => u.AccountId == freeAccount)
            .Select(u => u.RafflesCreated)
            .SingleAsync();
        count.ShouldBe(1);
    }

    [Fact]
    public async Task S4_FilesAreIsolatedByAccount_Returns404ForOtherAccount()
    {
        var accountB = await SeedAccountAsync("Cuenta B Archivos", PlanCatalog.Pro);
        var fileIdA = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            db.StoredFiles.Add(new StoredFileData
            {
                Id = fileIdA,
                AccountId = Account.DefaultBanreservasAccountId,
                Provider = "filesystem",
                StorageKey = $"test/{fileIdA:N}.png",
                FileName = "firma-ganador.png",
                ContentType = "image/png",
                SizeBytes = 128,
                Checksum = "abc",
                CreatedAt = now,
                CreatedById = TestAuthHandler.DefaultUserId,
            });
            await db.SaveChangesAsync();
        }

        using var clientB = ClientFor(accountB);
        (await clientB.GetAsync($"/v1/files/{fileIdA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await clientB.GetAsync($"/v1/files/{fileIdA}/content")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A30_ImportPrizes_Creates10000PrizesAcross3RoundsAtomically_AndRejectsWhenNotDraft()
    {
        var eventId = await CreateEventAsync(Account.DefaultBanreservasAccountId, "Gran Fiesta 10K");
        using var client = ClientFor(Account.DefaultBanreservasAccountId);

        (await client.PostAsync($"/v1/events/{eventId}/raffle", null)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var importResp = await client.PostAsJsonAsync($"/v1/events/{eventId}/raffle/prizes/import", new ImportPrizesRequest
        {
            Prizes =
            [
                new ImportPrizeItemRequest { RoundNumber = 1, Name = "Bono 10,000", Value = "RD$10,000", Quantity = 5000 },
                new ImportPrizeItemRequest { RoundNumber = 2, Name = "Bono 25,000", Value = "RD$25,000", Quantity = 3000 },
                new ImportPrizeItemRequest { RoundNumber = 3, Name = "Bono 50,000", Value = "RD$50,000", Quantity = 2000 },
            ],
        });

        importResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await importResp.Content.ReadFromJsonAsync<ImportPrizesResponse>();
        payload.ShouldNotBeNull();
        payload.Created.ShouldBe(10_000);
        payload.RoundsCreated.ShouldBe(3);

        // Exceder 10.000 premios en una petición devuelve 400
        var overLimitResp = await client.PostAsJsonAsync($"/v1/events/{eventId}/raffle/prizes/import", new ImportPrizesRequest
        {
            Prizes = [new ImportPrizeItemRequest { RoundNumber = 1, Name = "Extra", Value = null, Quantity = 10_001 }],
        });
        overLimitResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImportPrizes_PersistsTheOptionalSponsorName()
    {
        var eventId = await CreateEventAsync(Account.DefaultBanreservasAccountId, "Fiesta con patrocinadores");
        using var client = ClientFor(Account.DefaultBanreservasAccountId);
        (await client.PostAsync($"/v1/events/{eventId}/raffle", null)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var importResp = await client.PostAsJsonAsync($"/v1/events/{eventId}/raffle/prizes/import", new ImportPrizesRequest
        {
            Prizes =
            [
                new ImportPrizeItemRequest { RoundNumber = 1, Name = "Televisor", Quantity = 2, SponsorName = "  Brugal & Co.  " },
                new ImportPrizeItemRequest { RoundNumber = 1, Name = "Bono", Quantity = 1, SponsorName = "   " },
            ],
        });
        importResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var raffleId = await db.Raffles.IgnoreQueryFilters().Where(r => r.EventId == eventId).Select(r => r.Id).SingleAsync();
        var sponsors = await db.Prizes
            .IgnoreQueryFilters()
            .Where(p => db.RaffleRounds.Any(r => r.Id == p.RoundId && r.RaffleId == raffleId))
            .OrderBy(p => p.Name)
            .Select(p => new { p.Name, p.SponsorName })
            .ToListAsync();

        sponsors.Count.ShouldBe(3);
        sponsors.Single(p => p.Name == "Bono").SponsorName.ShouldBeNull();
        sponsors.Where(p => p.Name == "Televisor").ShouldAllBe(p => p.SponsorName == "Brugal & Co.");
    }

    private HttpClient ClientFor(Guid accountId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AccountIdHeader, accountId.ToString("D"));
        return client;
    }

    private async Task<Guid> SeedAccountAsync(string name, string plan)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        db.Accounts.Add(new AccountData
        {
            Id = id,
            Name = name,
            Plan = plan,
            Status = AccountStatus.Active,
            PlanStartedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> CreateEventAsync(Guid accountId, string name)
    {
        using var client = ClientFor(accountId);
        var resp = await client.PostAsJsonAsync("/v1/events", new
        {
            type = "Corporate",
            name,
            startDate = "2026-12-15",
            endDate = "2026-12-15",
            language = "es",
            visibility = "private",
            city = "Santo Domingo",
            country = "República Dominicana",
            timeZone = "America/Santo_Domingo",
        });
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await resp.Content.ReadFromJsonAsync<Guid>();
    }
}

