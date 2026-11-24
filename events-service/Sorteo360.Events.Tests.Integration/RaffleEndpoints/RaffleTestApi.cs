using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// Lo que comparten las pruebas del sorteo, sus rondas y sus premios: armar un evento con sorteo,
/// leerlo, llevar el evento a un estado terminal y mandar pedidos con otros roles.
/// </summary>
internal sealed class RaffleTestApi
{
    private readonly EventsApiDbFactory _factory;
    private readonly HttpClient _client;

    public RaffleTestApi(EventsApiDbFactory factory, HttpClient client)
    {
        _factory = factory;
        _client = client;
    }

    public static string RaffleUrl(Guid eventId) => $"v1/events/{eventId}/raffle";

    public async Task<RaffleResponse> GetRaffleAsync(Guid eventId)
    {
        var get = await _client.GetAsync(RaffleUrl(eventId));
        get.StatusCode.ShouldBe(HttpStatusCode.OK, await get.Content.ReadAsStringAsync());
        return (await get.Content.ReadFromJsonAsync<RaffleResponse>())!;
    }

    public static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync());
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().ShouldBe(code);
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content, string roles)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add(TestAuthHandler.RolesHeader, roles);
        return _client.SendAsync(request);
    }

    public async Task<Guid> CreateEventAsync(string type = EventType.Roadshow)
    {
        var request = new CreateEventRequest
        {
            Type      = type,
            Name      = "Roadshow del sorteo",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate   = new DateOnly(2026, 10, 5),
            City      = "Santo Domingo",
            Country   = "Dominican Republic"
        };
        var response = await _client.PostAsJsonAsync("v1/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    public async Task<Guid> CreateEventWithRaffleAsync(string type = EventType.Roadshow)
    {
        var eventId = await CreateEventAsync(type);
        var create = await _client.PostAsync(RaffleUrl(eventId), content: null);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        return eventId;
    }

    /// <summary>
    /// Lleva el evento a un estado terminal directo en la base: cerrarlo por la API exige
    /// cotizaciones aprobadas, y lo que se prueba acá es solo cómo reacciona el sorteo.
    /// </summary>
    public async Task SetEventStatusAsync(Guid eventId, string status)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE events.event SET status = {status} WHERE id = {eventId}");
    }

    public static string AttendeesUrl(Guid eventId) => $"{RaffleUrl(eventId)}/attendees";

    public static string OverridesUrl(Guid eventId) => $"{AttendeesUrl(eventId)}/overrides";

    public async Task<RaffleAttendeesResponse> GetAttendeesAsync(Guid eventId)
    {
        var get = await _client.GetAsync(AttendeesUrl(eventId));
        get.StatusCode.ShouldBe(HttpStatusCode.OK, await get.Content.ReadAsStringAsync());
        return (await get.Content.ReadFromJsonAsync<RaffleAttendeesResponse>())!;
    }

    public Task<HttpResponseMessage> SetOverrideAsync(Guid eventId, string email, string decision, string? note = null) =>
        _client.PostAsJsonAsync(
            OverridesUrl(eventId), new SetAttendeeOverrideRequest { Email = email, Decision = decision, Note = note });

    public Task<HttpResponseMessage> RemoveOverrideAsync(Guid eventId, string email) =>
        _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, OverridesUrl(eventId))
        {
            Content = JsonContent.Create(new RemoveAttendeeOverrideRequest { Email = email }),
        });

    /// <summary>
    /// Las decisiones manuales guardadas del sorteo del evento, leídas directo de la tabla: lo que
    /// un rechazo no tiene que haber tocado.
    /// </summary>
    public async Task<IReadOnlyList<StoredOverride>> GetStoredOverridesAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        return await context.Database
            .SqlQuery<StoredOverride>(
                $"""
                SELECT o.email_normalized AS "Email", o.decision AS "Decision", o.note AS "Note",
                       o.decided_by_id AS "DecidedById", o.decided_at AS "DecidedAt"
                FROM events.raffle_attendee_override o
                JOIN events.raffle r ON r.id = o.raffle_id
                WHERE r.event_id = {eventId}
                ORDER BY o.email_normalized
                """)
            .ToListAsync();
    }

    /// <summary>
    /// Lleva el sorteo a otro estado directo en la base, sin pasar por <c>start</c>, que exige una
    /// puerta con alguien que califique.
    /// </summary>
    public async Task SetRaffleStateAsync(Guid eventId, string state)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        // Completado lleva quién y cuándo, y nada más los lleva (ck_raffle_completed).
        var closed = state == RaffleState.Completed;
        DateTimeOffset? completedAt = closed ? DateTimeOffset.UtcNow : null;
        Guid? completedById = closed ? Guid.NewGuid() : null;

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE events.raffle
            SET state = {state}, completed_at = {completedAt}, completed_by_id = {completedById}
            WHERE event_id = {eventId}
            """);
    }

    public static string StartUrl(Guid eventId) => $"{RaffleUrl(eventId)}/start";

    public Task<HttpResponseMessage> StartAsync(Guid eventId) => _client.PostAsync(StartUrl(eventId), content: null);

    /// <summary>
    /// Lo que el arranque escribió en el sorteo, leído directo de la tabla: lo que un arranque
    /// rechazado no tiene que haber tocado.
    /// </summary>
    public async Task<StoredRaffle> GetStoredRaffleAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        return await context.Database
            .SqlQuery<StoredRaffle>(
                $"""
                SELECT state AS "State", started_at AS "StartedAt", started_by_id AS "StartedById",
                       awards_batch_id AS "AwardsBatchId"
                FROM events.raffle
                WHERE event_id = {eventId}
                """)
            .SingleAsync();
    }

    /// <summary>Las filas congeladas del sorteo del evento, leídas directo de la tabla.</summary>
    public async Task<IReadOnlyList<StoredAttendee>> GetStoredAttendeesAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        return await context.Database
            .SqlQuery<StoredAttendee>(
                $"""
                SELECT a.email_normalized AS "Email", a.contact_id AS "ContactId", a.qualified AS "Qualified",
                       a.exclusion_reason AS "ExclusionReason", a.manual_decision AS "ManualDecision",
                       a.manual_decided_by_id AS "ManualDecidedById", a.manual_note AS "ManualNote",
                       a.overruled_reason AS "OverruledReason"
                FROM events.raffle_attendee a
                JOIN events.raffle r ON r.id = a.raffle_id
                WHERE r.event_id = {eventId}
                ORDER BY a.email_normalized
                """)
            .ToListAsync();
    }

    public static string RoundsUrl(Guid eventId) => $"{RaffleUrl(eventId)}/rounds";

    public static string PrizesUrl(Guid eventId) => $"{RaffleUrl(eventId)}/prizes";

    public async Task<Guid> CreateRoundAsync(Guid eventId, string name = "Ronda")
    {
        var response = await _client.PostAsJsonAsync(RoundsUrl(eventId), new CreateRaffleRoundRequest { Name = name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    public async Task<IReadOnlyList<PrizeResponse>> CreatePrizesAsync(
        Guid eventId, Guid roundId, string name = "Bolsa", int quantity = 1,
        Guid? sponsorCompanyId = null, Guid? sponsorContactId = null, string? sponsorName = null)
    {
        var response = await _client.PostAsJsonAsync(PrizesUrl(eventId), new CreatePrizesRequest
        {
            RoundId = roundId,
            Name = name,
            Quantity = quantity,
            SponsorCompanyId = sponsorCompanyId,
            SponsorContactId = sponsorContactId,
            SponsorName = sponsorName,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<List<PrizeResponse>>())!;
    }

    public static string PrizeUrl(Guid eventId, Guid prizeId) => $"{PrizesUrl(eventId)}/{prizeId}";

    public static string DrawUrl(Guid eventId, Guid prizeId) => $"{PrizeUrl(eventId, prizeId)}/draw";

    public static string VoidUrl(Guid eventId, Guid prizeId) => $"{PrizeUrl(eventId, prizeId)}/void";

    public static string ReopenUrl(Guid eventId) => $"{RaffleUrl(eventId)}/reopen";

    public Task<HttpResponseMessage> DrawAsync(Guid eventId, Guid prizeId) => _client.PostAsync(DrawUrl(eventId, prizeId), content: null);

    public Task<HttpResponseMessage> VoidAsync(Guid eventId, Guid prizeId, string? code, string? note = null) =>
        _client.PostAsJsonAsync(VoidUrl(eventId, prizeId), new VoidPrizeDrawRequest { Code = code, Note = note });

    public Task<HttpResponseMessage> ReopenAsync(Guid eventId) => _client.PostAsync(ReopenUrl(eventId), content: null);

    public static string CompleteUrl(Guid eventId) => $"{RaffleUrl(eventId)}/complete";

    /// <summary>
    /// Completa y, si sale bien, espera los correos de los ganadores, que van en segundo plano: así
    /// ningún correo atrasado cae en el registro del vecino de la prueba siguiente.
    /// </summary>
    public async Task<HttpResponseMessage> CompleteAsync(Guid eventId)
    {
        var response = await _client.PostAsync(CompleteUrl(eventId), content: null);
        if (response.IsSuccessStatusCode)
            await WaitForWinnerEmailsAsync(eventId);

        return response;
    }

    /// <summary>Espera a que cada sorteo vivo del evento tenga el desenlace de su correo guardado.</summary>
    public async Task WaitForWinnerEmailsAsync(Guid eventId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while ((await GetStoredDrawsAsync(eventId)).Any(d => d.VoidedAt is null && d.WinnerNotificationStatus is null))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Los correos de los ganadores no salieron a tiempo.");

            await Task.Delay(50);
        }
    }

    public static string ReissueUrl(Guid eventId) => $"{RaffleUrl(eventId)}/reissue";

    public Task<HttpResponseMessage> ReissueAsync(Guid eventId, ReissueRaffleRequest body) =>
        _client.PostAsJsonAsync(ReissueUrl(eventId), body);

    /// <summary>
    /// Las seis autoridades de <see cref="ReadyToCompleteUpdate"/> con el documento del segundo testigo
    /// corregido: dos dígitos que se habían transpuesto (spec 03 §4.5).
    /// </summary>
    public static ReissueRaffleRequest CorrectedAuthorities() => new()
    {
        NotaryName = "Ana Pérez",
        NotaryIdentityDocument = "001-0000001-1",
        WitnessOneName = "Luis Gómez",
        WitnessOneIdentityDocument = "001-0000002-2",
        WitnessTwoName = "María Díaz",
        WitnessTwoIdentityDocument = "001-0000030-3",
    };

    /// <summary>
    /// El cuerpo del PATCH con los ajustes por omisión, las seis autoridades y las instrucciones de
    /// retiro: lo que completar exige. Las reglas son las de un sorteo nuevo, así que sirve también en curso.
    /// </summary>
    public static UpdateRaffleRequest ReadyToCompleteUpdate() => new()
    {
        VisualStyle = "roulette",
        ShowPrizeOnScreen = true,
        AnimationSeconds = 6,
        RequiresPresenceConfirmation = false,
        OnePrizePerPerson = true,
        OnePrizePerCompany = false,
        NotaryName = "Ana Pérez",
        NotaryIdentityDocument = "001-0000001-1",
        WitnessOneName = "Luis Gómez",
        WitnessOneIdentityDocument = "001-0000002-2",
        WitnessTwoName = "María Díaz",
        WitnessTwoIdentityDocument = "001-0000003-3",
        PickupInstructions = "Stand de MITUR, hasta el viernes a las 18:00.",
    };

    public async Task PatchRaffleAsync(Guid eventId, UpdateRaffleRequest body)
    {
        var patch = await _client.PatchAsJsonAsync(RaffleUrl(eventId), body);
        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent, await patch.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Un sorteo arrancado, con un premio sin sortear, las autoridades y las instrucciones de retiro:
    /// lo único que le falta es completarlo.
    /// </summary>
    public async Task<(Guid EventId, Guid PrizeId)> ReadyToCompleteAsync(FakeAttendanceSource attendance)
    {
        var (eventId, prizes) = await StartedRaffleAsync(attendance, people: 2, prizes: 1);
        await PatchRaffleAsync(eventId, ReadyToCompleteUpdate());
        return (eventId, prizes[0]);
    }

    /// <summary>Las versiones del acta del sorteo del evento, leídas directo de la tabla.</summary>
    public async Task<IReadOnlyList<StoredNotarialRecord>> GetStoredNotarialRecordsAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        return await context.Database
            .SqlQuery<StoredNotarialRecord>(
                $"""
                SELECT n.version AS "Version", n.stored_file_id AS "StoredFileId",
                       n.generated_at AS "GeneratedAt", n.generated_by_id AS "GeneratedById"
                FROM events.notarial_record n
                JOIN events.raffle r ON r.id = n.raffle_id
                WHERE r.event_id = {eventId}
                ORDER BY n.version
                """)
            .ToListAsync();
    }

    /// <summary>
    /// Un sorteo arrancado con tantos invitados calificados como se pidan, todos de la misma agencia, y
    /// tantos premios en una ronda. Con los valores por omisión: un premio por persona sí y por empresa no.
    /// </summary>
    public async Task<(Guid EventId, IReadOnlyList<Guid> PrizeIds)> StartedRaffleAsync(
        FakeAttendanceSource attendance, int people, int prizes)
    {
        var eventId = await CreateEventWithRaffleAsync();
        attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(people));
        var start = await StartAsync(eventId);
        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());

        var created = await CreatePrizesAsync(eventId, await CreateRoundAsync(eventId), quantity: prizes);
        return (eventId, created.Select(p => p.Id).ToList());
    }

    public static string HandoverUrl(Guid eventId, Guid prizeId) => $"{PrizeUrl(eventId, prizeId)}/handover";

    public static string ReverseHandoverUrl(Guid eventId, Guid prizeId) => $"{HandoverUrl(eventId, prizeId)}/reverse";

    /// <summary>
    /// El formulario de una entrega. Un campo en <c>null</c> no viaja, que es como se prueba su
    /// ausencia; por omisión el ganador retira con su documento y una firma PNG.
    /// </summary>
    public static MultipartFormDataContent HandoverForm(
        string? receiverName = "María Pérez",
        string? receiverIdentityDocument = "001-1234567-8",
        string? receiverIsWinner = "true",
        byte[]? signature = null,
        string signatureFileName = "firma.png",
        string signatureContentType = "image/png",
        bool withSignature = true)
    {
        var form = new MultipartFormDataContent();
        if (receiverName is not null)
            form.Add(new StringContent(receiverName), "receiverName");
        if (receiverIdentityDocument is not null)
            form.Add(new StringContent(receiverIdentityDocument), "receiverIdentityDocument");
        if (receiverIsWinner is not null)
            form.Add(new StringContent(receiverIsWinner), "receiverIsWinner");

        if (withSignature)
        {
            var file = new ByteArrayContent(signature ?? SampleFiles.Png());
            file.Headers.ContentType = new MediaTypeHeaderValue(signatureContentType);
            form.Add(file, "signature", signatureFileName);
        }

        return form;
    }

    public Task<HttpResponseMessage> HandOverAsync(Guid eventId, Guid prizeId, MultipartFormDataContent? form = null) =>
        _client.PostAsync(HandoverUrl(eventId, prizeId), form ?? HandoverForm());

    public Task<HttpResponseMessage> ReverseHandoverAsync(Guid eventId, Guid prizeId, string? reason = "Premio equivocado") =>
        _client.PostAsJsonAsync(ReverseHandoverUrl(eventId, prizeId), new ReversePrizeHandoverRequest { Reason = reason });

    /// <summary>Un sorteo arrancado con un premio ya sorteado.</summary>
    public async Task<(Guid EventId, Guid PrizeId)> DrawnPrizeAsync(FakeAttendanceSource attendance)
    {
        var (eventId, prizes) = await StartedRaffleAsync(attendance, people: 2, prizes: 1);
        var draw = await DrawAsync(eventId, prizes[0]);
        draw.StatusCode.ShouldBe(HttpStatusCode.OK, await draw.Content.ReadAsStringAsync());
        return (eventId, prizes[0]);
    }

    /// <summary>Las entregas del sorteo del evento, vivas y revertidas, leídas directo de la tabla.</summary>
    public async Task<IReadOnlyList<StoredHandover>> GetStoredHandoversAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        return await context.Database
            .SqlQuery<StoredHandover>(
                $"""
                SELECT h.id AS "Id", h.prize_draw_id AS "PrizeDrawId", h.receiver_name AS "ReceiverName",
                       h.receiver_identity_document AS "ReceiverIdentityDocument",
                       h.receiver_is_winner AS "ReceiverIsWinner",
                       h.signature_stored_file_id AS "SignatureStoredFileId", h.redeemed_at AS "RedeemedAt",
                       h.redeemed_by_id AS "RedeemedById", h.reversed_at AS "ReversedAt",
                       h.reversed_by_id AS "ReversedById", h.reversal_reason AS "ReversalReason"
                FROM events.prize_handover h
                JOIN events.prize_draw d ON d.id = h.prize_draw_id
                JOIN events.prize p ON p.id = d.prize_id
                JOIN events.raffle_round rr ON rr.id = p.round_id
                JOIN events.raffle r ON r.id = rr.raffle_id
                WHERE r.event_id = {eventId}
                ORDER BY h.redeemed_at, h.id
                """)
            .ToListAsync();
    }

    /// <summary>Cuántas filas tiene <c>stored_file</c>, de cualquier dueño.</summary>
    public async Task<int> CountStoredFileRowsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        return await context.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM events.stored_file")
            .SingleAsync();
    }

    /// <summary>Cuántos archivos hay de verdad en el almacenamiento.</summary>
    public int CountStoredBytes() =>
        Directory.Exists(_factory.FileStoreRootPath)
            ? Directory.EnumerateFiles(_factory.FileStoreRootPath, "*", SearchOption.AllDirectories).Count()
            : 0;

    /// <summary>Los sorteos de premio del sorteo del evento, vivos y anulados, leídos directo de la tabla.</summary>
    /// <summary>
    /// Espera a que alguna sesión de la base quede esperando un bloqueo: la escritura que el test
    /// disparó ya llegó al <c>FOR UPDATE</c> y está detenida ahí.
    /// </summary>
    public async Task WaitUntilSomebodyWaitsOnALockAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var waiting = await context.Database
                .SqlQuery<int>(
                    $"""
                    SELECT count(*)::int AS "Value" FROM pg_stat_activity
                    WHERE wait_event_type = 'Lock' AND datname = current_database()
                    """)
                .SingleAsync();
            if (waiting > 0)
                return;

            await Task.Delay(20);
        }

        throw new TimeoutException("La escritura nunca llegó a esperar el bloqueo del sorteo.");
    }

    public async Task<IReadOnlyList<StoredDraw>> GetStoredDrawsAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        return await context.Database
            .SqlQuery<StoredDraw>(
                $"""
                SELECT d.id AS "Id", d.prize_id AS "PrizeId", d.redemption_code AS "RedemptionCode",
                       d.void_code AS "VoidCode", d.void_note AS "VoidNote", d.voided_at AS "VoidedAt",
                       d.voided_by_id AS "VoidedById", d.winner_name AS "WinnerName",
                       d.winner_email AS "WinnerEmail",
                       d.winner_notification_status AS "WinnerNotificationStatus",
                       d.winner_notification_id AS "WinnerNotificationId",
                       d.winner_attendee_id AS "WinnerAttendeeId"
                FROM events.prize_draw d
                JOIN events.prize p ON p.id = d.prize_id
                JOIN events.raffle_round rr ON rr.id = p.round_id
                JOIN events.raffle r ON r.id = rr.raffle_id
                WHERE r.event_id = {eventId}
                ORDER BY d.drawn_at, d.id
                """)
            .ToListAsync();
    }
}

/// <summary>Una fila de <c>notarial_record</c>, tal como está en la base.</summary>
internal sealed record StoredNotarialRecord(int Version, Guid StoredFileId, DateTimeOffset GeneratedAt, Guid GeneratedById);

/// <summary>Una fila de <c>prize_draw</c>, tal como está en la base.</summary>
internal sealed record StoredDraw(
    Guid Id,
    Guid PrizeId,
    string RedemptionCode,
    string? VoidCode,
    string? VoidNote,
    DateTimeOffset? VoidedAt,
    Guid? VoidedById,
    string WinnerName,
    string WinnerEmail,
    string? WinnerNotificationStatus,
    string? WinnerNotificationId,
    Guid WinnerAttendeeId);

/// <summary>Una fila de <c>prize_handover</c>, tal como está en la base.</summary>
internal sealed record StoredHandover(
    Guid Id,
    Guid PrizeDrawId,
    string ReceiverName,
    string ReceiverIdentityDocument,
    bool ReceiverIsWinner,
    Guid SignatureStoredFileId,
    DateTimeOffset RedeemedAt,
    Guid RedeemedById,
    DateTimeOffset? ReversedAt,
    Guid? ReversedById,
    string? ReversalReason);

/// <summary>Una fila de <c>raffle_attendee_override</c>, tal como está en la base.</summary>
internal sealed record StoredOverride(string Email, string Decision, string? Note, Guid DecidedById, DateTimeOffset DecidedAt);

/// <summary>Las columnas del arranque en <c>raffle</c>, tal como están en la base.</summary>
internal sealed record StoredRaffle(string State, DateTimeOffset? StartedAt, Guid? StartedById, Guid? AwardsBatchId);

/// <summary>Una fila de <c>raffle_attendee</c>, tal como está en la base.</summary>
internal sealed record StoredAttendee(
    string Email,
    Guid? ContactId,
    bool Qualified,
    string? ExclusionReason,
    string? ManualDecision,
    Guid? ManualDecidedById,
    string? ManualNote,
    string? OverruledReason);
