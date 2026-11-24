using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Shouldly;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.RaffleEndpoints;
using Xunit.Abstractions;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Prueba de escala A31: 25.000 participantes y 10.000 premios contra PostgreSQL real (Testcontainers),
/// una métrica por fila de <c>plans/pruebas-y-produccion.md</c> §3.3. No corre en CI normal:
/// <c>dotnet test --filter Category=Scale</c>. Las puertas corren 60 s; <c>SCALE_DOOR_SECONDS=600</c>
/// da los 10 minutos de la tabla. Anota todas las cifras y falla al final con las que no llegaron.
/// </summary>
[Trait("Category", "Scale")]
public sealed class RaffleScaleTests : IClassFixture<RaffleScaleTests.ScaleApiDbFactory>
{
    private const int People = 25_000;
    private const int NotCheckedIn = 1_000;
    private const int SingleSpins = 100;
    private const int Doors = 5;

    private readonly ScaleApiDbFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly List<(string Operation, string Goal, string Measured, bool Passed)> _metrics = [];

    public RaffleScaleTests(ScaleApiDbFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.Realm.ServeDefaults();
        _output = output;
    }

    [Fact]
    public async Task Scale_25000_Participants_And_10000_Prizes_Meet_Performance_Targets()
    {
        using var client = _factory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        var api = new RaffleTestApi(_factory, client);
        var eventId = await api.CreateEventWithRaffleAsync();
        var url = RaffleTestApi.RaffleUrl(eventId);

        // Importar 25.000 en una petición; los últimos 1.000 llegan sin check-in para la puerta.
        var participants = Enumerable.Range(1, People)
            .Select(i => new RaffleParticipantInputDto(
                FullName: $"Colaborador {i}",
                Email: $"emp{i}@banreservas.example",
                Department: $"Area {(i % 50) + 1}",
                EmployeeCode: $"EMP-{i:D5}",
                Company: "Banreservas",
                CheckedIn: i <= People - NotCheckedIn))
            .ToList();
        var sw = Stopwatch.StartNew();
        var import = await client.PostAsJsonAsync($"{url}/participants/import",
            new ImportRaffleParticipantsHttpRequest(participants, ReplaceExisting: true, Source: "excel"));
        sw.Stop();
        import.StatusCode.ShouldBe(HttpStatusCode.OK, await import.Content.ReadAsStringAsync());
        Record("Importar 25.000 participantes (una petición)", "< 10 s", sw.Elapsed, TimeSpan.FromSeconds(10));

        sw.Restart();
        var list = await client.GetAsync($"{url}/participants");
        var listed = (await list.Content.ReadFromJsonAsync<List<RaffleParticipantDto>>())!;
        sw.Stop();
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        listed.Count.ShouldBe(People);
        Record("Listar el padrón", "< 1,5 s", sw.Elapsed, TimeSpan.FromSeconds(1.5));

        // Check-in por QR o boleto: cada uno de los que faltan, de a uno, por su código.
        var checkIns = new List<TimeSpan>();
        foreach (var p in listed.Where(p => !p.CheckedIn).Take(200))
        {
            sw.Restart();
            var checkIn = await client.PostAsJsonAsync($"{url}/participants/check-in",
                new CheckInRaffleParticipantHttpRequest(Code: p.QrCode));
            sw.Stop();
            checkIn.StatusCode.ShouldBe(HttpStatusCode.OK, await checkIn.Content.ReadAsStringAsync());
            checkIns.Add(sw.Elapsed);
        }
        RecordLatencies("Check-in por QR o boleto", "< 100 ms", checkIns, TimeSpan.FromMilliseconds(100));

        await DoorsAsync(url, listed.Select(p => p.QrCode).ToArray());

        var prizeImport = await client.PostAsJsonAsync($"{RaffleTestApi.PrizesUrl(eventId)}/import", new ImportPrizesRequest
        {
            Prizes = Enumerable.Range(1, 20)
                .Select(r => new ImportPrizeItemRequest { RoundNumber = r, Name = $"Bono Ronda {r}", Value = "RD$10,000", Quantity = 500 })
                .ToList(),
        });
        prizeImport.StatusCode.ShouldBe(HttpStatusCode.OK, await prizeImport.Content.ReadAsStringAsync());

        sw.Restart();
        var get = await client.GetAsync(url);
        var raffle = (await get.Content.ReadFromJsonAsync<RaffleResponse>())!;
        sw.Stop();
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var prizeIds = raffle.Rounds.SelectMany(r => r.Prizes).Select(p => p.Id).ToList();
        prizeIds.Count.ShouldBe(10_000);
        Record("Leer un sorteo con 10.000 premios", "< 1,5 s", sw.Elapsed, TimeSpan.FromSeconds(1.5));

        using (var conditional = new HttpRequestMessage(HttpMethod.Get, url))
        {
            conditional.Headers.TryAddWithoutValidation("If-None-Match", get.Headers.ETag!.ToString());
            (await client.SendAsync(conditional)).StatusCode.ShouldBe(HttpStatusCode.NotModified);
        }

        await api.PatchRaffleAsync(eventId, RaffleTestApi.ReadyToCompleteUpdate());
        sw.Restart();
        var start = await api.StartAsync(eventId);
        sw.Stop();
        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());
        Info("Iniciar (congela 25.000)", sw.Elapsed);

        var spins = new List<TimeSpan>();
        foreach (var prizeId in prizeIds.Take(SingleSpins))
        {
            sw.Restart();
            var draw = await api.DrawAsync(eventId, prizeId);
            sw.Stop();
            draw.StatusCode.ShouldBe(HttpStatusCode.OK, await draw.Content.ReadAsStringAsync());
            spins.Add(sw.Elapsed);
        }
        RecordLatencies("Cada giro", "< 300 ms", spins, TimeSpan.FromMilliseconds(300));

        // El resto en lotes de 10, para que el acta lleve los 10.000 ganadores.
        var batches = new List<TimeSpan>();
        var fill = Stopwatch.StartNew();
        foreach (var chunk in prizeIds.Skip(SingleSpins).Chunk(10))
        {
            sw.Restart();
            var batch = await client.PostAsJsonAsync($"{RaffleTestApi.PrizesUrl(eventId)}/draw-batch",
                new DrawBatchPrizesRequest { PrizeIds = chunk });
            sw.Stop();
            batch.StatusCode.ShouldBe(HttpStatusCode.OK, await batch.Content.ReadAsStringAsync());
            batches.Add(sw.Elapsed);
        }
        fill.Stop();
        _output.WriteLine($"INFO  Lote de 10 giros: p50 {Ms(Pct(batches, 50))}, p95 {Ms(Pct(batches, 95))}, " +
                          $"máx {Ms(batches.Max())}; {batches.Count} lotes en {fill.Elapsed.TotalSeconds:F1} s");

        GC.Collect();
        var before = Process.GetCurrentProcess();
        var heapBefore = GC.GetTotalMemory(forceFullCollection: true);
        sw.Restart();
        // Directo y no por RaffleTestApi, que espera los correos: los 10.000 salen en segundo plano.
        var complete = await client.PostAsync(RaffleTestApi.CompleteUrl(eventId), content: null);
        var completed = (await complete.Content.ReadFromJsonAsync<RaffleResponse>())!;
        sw.Stop();
        complete.StatusCode.ShouldBe(HttpStatusCode.OK);
        before.Refresh();
        var pdf = await client.GetByteArrayAsync($"v1/files/{completed.NotarialRecords[0].StoredFileId}/content");
        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
        Record("Acta en PDF con 10.000 premios", "< 30 s, sin quedarse sin memoria", sw.Elapsed, TimeSpan.FromSeconds(30),
            $"; PDF {pdf.Length / 1024.0 / 1024.0:F1} MB, pico del proceso {before.PeakWorkingSet64 / 1024 / 1024} MB " +
            $"(heap antes {heapBefore / 1024 / 1024} MB)");

        _output.WriteLine("");
        _output.WriteLine("| Operación | Meta | Medido | Pasa |");
        _output.WriteLine("| --- | --- | --- | --- |");
        foreach (var m in _metrics)
            _output.WriteLine($"| {m.Operation} | {m.Goal} | {m.Measured} | {(m.Passed ? "sí" : "no")} |");

        _metrics.Where(m => !m.Passed).Select(m => $"{m.Operation}: {m.Measured} (meta {m.Goal})").ShouldBeEmpty();
    }

    /// <summary>
    /// Cinco puertas escaneando a la vez, cada una con su cliente, sobre códigos al azar del padrón.
    /// La meta es cero 5xx; se anota también la latencia.
    /// </summary>
    private async Task DoorsAsync(string url, string[] codes)
    {
        var seconds = int.TryParse(Environment.GetEnvironmentVariable("SCALE_DOOR_SECONDS"), out var s) ? s : 60;
        var until = DateTime.UtcNow.AddSeconds(seconds);
        var latencies = new ConcurrentBag<TimeSpan>();
        var errors = new ConcurrentBag<string>();

        await Task.WhenAll(Enumerable.Range(0, Doors).Select(door => Task.Run(async () =>
        {
            using var client = _factory.CreateClient();
            var random = new Random(door);
            while (DateTime.UtcNow < until)
            {
                var sw = Stopwatch.StartNew();
                var response = await client.PostAsJsonAsync($"{url}/participants/check-in",
                    new CheckInRaffleParticipantHttpRequest(Code: codes[random.Next(codes.Length)]));
                sw.Stop();
                latencies.Add(sw.Elapsed);
                if ((int)response.StatusCode >= 500)
                    errors.Add($"{(int)response.StatusCode}");
            }
        })));

        var all = latencies.ToList();
        Add($"{Doors} puertas escaneando a la vez durante {seconds} s", "Sin errores 5xx",
            $"{errors.Count} errores 5xx en {all.Count} escaneos; p50 {Ms(Pct(all, 50))}, p95 {Ms(Pct(all, 95))}, máx {Ms(all.Max())}",
            errors.IsEmpty);
    }

    private void Record(string operation, string goal, TimeSpan measured, TimeSpan limit, string extra = "") =>
        Add(operation, goal, $"{Fmt(measured)}{extra}", measured < limit);

    private void RecordLatencies(string operation, string goal, List<TimeSpan> samples, TimeSpan limit) =>
        Add(operation, goal,
            $"p50 {Ms(Pct(samples, 50))}, p95 {Ms(Pct(samples, 95))}, máx {Ms(samples.Max())} ({samples.Count} muestras)",
            Pct(samples, 95) < limit);

    /// <summary>Se imprime al medir: si un paso posterior revienta, las cifras anteriores quedan.</summary>
    private void Add(string operation, string goal, string measured, bool passed)
    {
        _metrics.Add((operation, goal, measured, passed));
        _output.WriteLine($"MÉTRICA {operation} | {goal} | {measured} | {(passed ? "sí" : "no")}");
    }

    private void Info(string operation, TimeSpan elapsed) => _output.WriteLine($"INFO  {operation}: {Fmt(elapsed)}");

    private static TimeSpan Pct(List<TimeSpan> samples, int percentile)
    {
        var sorted = samples.Order().ToList();
        return sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1)];
    }

    private static string Ms(TimeSpan t) => $"{t.TotalMilliseconds:F0} ms";

    private static string Fmt(TimeSpan t) => t.TotalSeconds >= 1 ? $"{t.TotalSeconds:F2} s" : Ms(t);

    /// <summary>
    /// La aplicación con un realm que contesta, como en producción: sin él, cada giro y cada lectura
    /// pagan los reintentos contra un Keycloak que no existe y la cifra no dice nada.
    /// </summary>
    public sealed class ScaleApiDbFactory : EventsApiDbFactory
    {
        public KeycloakRealmStub Realm { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Services:Keycloak:BaseUrl", Realm.Url);
            builder.UseSetting("QuoteApprovers:Realm", "sorteo360");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Realm.Dispose();

            base.Dispose(disposing);
        }
    }
}
