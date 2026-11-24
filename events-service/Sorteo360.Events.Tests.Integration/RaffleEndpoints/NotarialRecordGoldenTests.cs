using System.Net;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.CityEndpoints;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;
using UglyToad.PdfPig;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// La prueba dorada del acta (ADR-0024): una noche completa por la API, el PDF que guardó
/// <c>complete</c> leído de vuelta como texto, y en ese texto una marca por cada punto de spec 03
/// §4.2, en orden, más lo que no tiene que estar (§4.3). Se afirman subcadenas en orden y no el texto
/// entero, así que un cambio de corte de línea o de métricas de la fuente no la rompe.
/// </summary>
public sealed class NotarialRecordGoldenTests : IClassFixture<RaffleCompletionApiDbFactory>
{
    /// <summary>El operador que el realm conoce: decide la inclusión.</summary>
    private static readonly Guid KnownOperator = new("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// El arranque del sorteo en la madrugada del 2 de noviembre de 2025, cuando Chicago atrasa la hora
    /// a las 07:00 UTC.
    /// </summary>
    /// <remarks>
    /// El sorteo arranca a las 01:30 de Chicago (02:30 en Santo Domingo), el primer premio sale a las
    /// 01:50 y el segundo, veinte minutos después, a las 01:10 de la hora nueva.
    /// </remarks>
    private static readonly DateTimeOffset NightOfTheChange = new(2025, 11, 2, 6, 30, 0, TimeSpan.Zero);

    /// <summary>La marca del evento: el acta la nombra como organizador.</summary>
    private const string Organizer = "Banreservas";

    /// <summary>El patrocinador escrito a mano, sin empresa del CRM.</summary>
    private const string SponsorName = "Brugal & Co.";

    private const string SantoDomingoNote =
        "Las horas están en la hora local de Santo Domingo, Dominican Republic; cada una indica su diferencia con UTC.";

    private readonly RaffleCompletionApiDbFactory _factory;
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public NotarialRecordGoldenTests(RaffleCompletionApiDbFactory factory)
    {
        _factory = factory;
        _factory.Realm.Serve(new StubRealmUser(KnownOperator.ToString(), "luis", "luis@sorteo360.local", "Luis", "Gómez", null));
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task Complete_StoresARecordStatingEachItemInOrder_WhenTheEveningHadEverything()
    {
        var (number, text) = await RecordOfAFullEveningAsync();

        ShouldAppearInOrder(text,
            // 1. Título, número y versión
            "ACTA NOTARIAL DE SORTEO", $"Acta N.º {number} · Versión 1",
            // 2. El evento y su ciudad
            "Evento:", "Roadshow del sorteo", $"Organizador: {Organizer}", "Lugar: Santo Domingo, Dominican Republic",
            // 3. Las horas, en la hora local de la ciudad y con su diferencia con UTC
            "Inicio del sorteo y cierre de la lista de participantes:", "(hora de Santo Domingo, UTC\u221204:00)",
            "Cierre del sorteo:", "(hora de Santo Domingo, UTC\u221204:00)",
            // 4. Las reglas
            "Un premio por persona: sí", "Un premio por empresa: no",
            // 5. Las advertencias
            "no había una lista de DO Travel Awards en vigor",
            // 6. El método
            "Método de selección", "generador de números aleatorios criptográfico del sistema operativo",
            "se volvió a calcular antes de sortear cada premio",
            // 7. La aritmética
            "Registrados en la puerta: 6", "En el sorteo: 5", "Fuera del sorteo: 1", "excluidos por decisión manual: 1",
            // 8. Las decisiones manuales
            "Decisiones manuales", "Beto Báez (Agencia): exclusión manual del sorteo.",
            $"Decidió usuario no disponible ({TestAuthHandler.DefaultUserId})",
            // Desde A4 el .do califica: la inclusión ya no exceptúa ninguna regla.
            "Dalia Duarte (Agencia): inclusión manual en el sorteo. Decidió Luis Gómez",
            // 8 bis. Las reactivaciones tras una anulación
            "Reactivaciones tras anulación", "reactivación en el sorteo tras anulación. Decidió Luis Gómez",
            // 9. Los resultados por ronda; el patrocinador es texto libre (sponsorName)
            "Resultados por ronda", "Ronda 1: Primera ronda", "Bolsa:", $"Maleta, patrocinado por {SponsorName}: no adjudicado.",
            "Ronda 2: Ronda final", "Crucero:", "Sorpresa (añadido durante el sorteo):",
            "Premios adjudicados: 3. Premios no adjudicados: 1.",
            // 10. El registro cronológico
            "Registro cronológico de sorteos", "Anulado: otro motivo. Nota: Es empleado del hotel",
            // La nota que cierra el cuerpo: en qué reloj están las horas
            SantoDomingoNote,
            // 11. Las autoridades
            "Autoridades", "Notario: Ana Pérez, documento de identidad 001-0000001-1", "Firma:",
            "Primer testigo: Luis Gómez", "Segundo testigo: María Díaz",
            // 12. El anexo
            "Anexo: participantes del sorteo", "Las 5 personas que estaban en el sorteo",
            "Ana Álvarez", "Carla Castro", "Dalia Duarte", "Elena Espinal", "Fabio Núñez");
    }

    [Fact]
    public async Task Complete_StoresARecordWithoutWhatTheSpecLeavesOut_WhenTheEveningHadEverything()
    {
        var (_, text) = await RecordOfAFullEveningAsync();

        // Ni correos, ni códigos de retiro, ni sello o validador inventados, ni autoridades de relleno,
        // ni la palabra que el mock usaba para el azar, ni la nota de una decisión manual (spec 03 §4.3).
        text.ShouldNotContain("@");
        text.ShouldNotContain("RD-");
        text.ShouldNotContain("sello", Case.Insensitive);
        text.ShouldNotContain("validador", Case.Insensitive);
        text.ShouldNotContain("Notario Público Actuante");
        text.ShouldNotContain("Comité Organizador MITUR");
        text.ShouldNotContain("determinista", Case.Insensitive);
        text.ShouldNotContain("Pedido del cliente");
        text.ShouldNotContain("Lo pidió el director");

        // Beto quedó fuera a mano: está en las decisiones y no en el anexo.
        var annex = text[text.IndexOf("Anexo: participantes del sorteo", StringComparison.Ordinal)..];
        annex.ShouldNotContain("Beto Báez");
    }

    [Fact]
    public async Task Complete_StoresARecordNumberedOnEveryPage_WhenItSpansPages()
    {
        var (_, text) = await RecordOfAFullEveningAsync();

        text.ShouldContain("Página 1 de ");
    }

    [Fact]
    public async Task Reissue_StoresAVersionThatSaysWhatItReplacesAndKeepsTheOldOne_WhenAWitnessIsCorrected()
    {
        var (eventId, completed) = await CompleteAFullEveningAsync();
        var number = NotarialRecordContent.NumberOf(completed.Id);

        var reissue = await _api.ReissueAsync(eventId, CorrectedAuthorities());
        await ShouldBeOkAsync(reissue);
        var reissued = (await reissue.Content.ReadFromJsonAsync<RaffleResponse>())!;

        var v2 = await TextOfRecordAsync(reissued.NotarialRecords[0].StoredFileId);
        ShouldAppearInOrder(v2,
            "ACTA NOTARIAL DE SORTEO", $"Acta N.º {number} · Versión 2",
            "Versión 2. Reemplaza la versión 1, generada el", "(hora de Santo Domingo, UTC\u221204:00).",
            "Evento:", "Roadshow del sorteo",
            "Autoridades", "Segundo testigo: María Díaz, documento de identidad 001-0000030-3");
        v2.ShouldNotContain("001-0000003-3");

        var v1 = await TextOfRecordAsync(reissued.NotarialRecords[1].StoredFileId);
        v1.ShouldContain("001-0000003-3");
        v1.ShouldNotContain("Reemplaza");
    }

    [Fact]
    public async Task Complete_StoresEachTimeWithItsOwnOffset_WhenAChicagoEveningCrossesTheChange()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await PatchTheCityAsync(eventId, "Chicago", "United States", "America/Chicago");

        var completed = await CompleteAnEveningOnTheNightOfTheChangeAsync(eventId);
        var text = await TextOfRecordAsync(completed.NotarialRecords[0].StoredFileId);

        // Sin la diferencia con UTC, el segundo sorteo parecería anterior al primero.
        ShouldAppearInOrder(text,
            "Lugar: Chicago, United States",
            "Inicio del sorteo y cierre de la lista de participantes:",
            "2 de noviembre de 2025, 01:30 (hora de Chicago, UTC\u221205:00)",
            "Registro cronológico de sorteos",
            "02/11/2025 01:50:00", "(hora de Chicago, UTC\u221205:00)",
            "02/11/2025 01:10:00", "(hora de Chicago, UTC\u221206:00)",
            "Las horas están en la hora local de Chicago, United States; cada una indica su diferencia con UTC.",
            "Autoridades");
        text.ShouldNotContain("Santo Domingo");
    }

    /// <summary>
    /// Corregir la zona después de completar no cambia ningún momento, solo el reloj en que se
    /// escriben: la reemisión los dice en la hora corregida y la versión anterior queda como estaba.
    /// </summary>
    /// <remarks>
    /// La corrección pasa por el PATCH de la ciudad, que acepta un evento abierto de cualquier tipo;
    /// por eso se prueba en una Feria y no en un Roadshow (PGTI-335).
    /// </remarks>
    [Fact]
    public async Task Reissue_StoresTheCorrectedTimes_WhenTheCitysZoneIsCorrectedAfterCompletion()
    {
        var eventId = await _api.CreateEventWithRaffleAsync(EventType.Feria);
        await CompleteAnEveningOnTheNightOfTheChangeAsync(eventId);

        await PatchTheCityAsync(eventId, "Chicago", "United States", "America/Chicago");
        var reissue = await _api.ReissueAsync(eventId, CorrectedAuthorities());
        await ShouldBeOkAsync(reissue);
        var reissued = (await reissue.Content.ReadFromJsonAsync<RaffleResponse>())!;

        var v2 = await TextOfRecordAsync(reissued.NotarialRecords[0].StoredFileId);
        ShouldAppearInOrder(v2,
            "Versión 2. Reemplaza la versión 1",
            "Lugar: Chicago, United States",
            "2 de noviembre de 2025, 01:30 (hora de Chicago, UTC\u221205:00)");
        v2.ShouldNotContain("02:30");

        var v1 = await TextOfRecordAsync(reissued.NotarialRecords[1].StoredFileId);
        ShouldAppearInOrder(v1,
            "Lugar: Santo Domingo, Dominican Republic",
            "2 de noviembre de 2025, 02:30 (hora de Santo Domingo, UTC\u221204:00)",
            SantoDomingoNote);
    }

    /// <summary>
    /// Una regla que se apaga entre dos sorteos, el primero anulado: la sección de reglas lo dice en
    /// una frase y cada línea del registro, la anulada también, dice bajo qué reglas salió (spec 03
    /// §4.2 punto 4).
    /// </summary>
    [Fact]
    public async Task Complete_StoresARecordStatingEachDrawsRules_WhenARuleChangedBetweenDraws()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(3));
        var bolsa = (await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), "Bolsa"))[0].Id;
        await ShouldBeOkAsync(await _api.StartAsync(eventId));

        await ShouldBeOkAsync(await _api.DrawAsync(eventId, bolsa));
        await ShouldBeOkAsync(await _api.VoidAsync(eventId, bolsa, PrizeDrawVoidCode.Declined));
        // Un premio por persona ya no se apaga: la regla que cambia es la de empresa.
        var companyOn = ReadyToCompleteUpdate() with { OnePrizePerCompany = true };
        await _api.PatchRaffleAsync(eventId, companyOn);
        await ShouldBeOkAsync(await _api.DrawAsync(eventId, bolsa));
        var complete = await _api.CompleteAsync(eventId);
        await ShouldBeOkAsync(complete);
        var completed = (await complete.Content.ReadFromJsonAsync<RaffleResponse>())!;

        var text = await TextOfRecordAsync(completed.NotarialRecords[0].StoredFileId);

        ShouldAppearInOrder(text,
            "Reglas y advertencias", "Las reglas cambiaron durante el sorteo; cada sorteo indica las suyas.",
            "Registro cronológico de sorteos",
            "Anulado:", "Un premio por persona: sí. Un premio por empresa: no.",
            "Un premio por persona: sí. Un premio por empresa: sí.",
            "Autoridades");
        // Solo las dos líneas del registro: la sección de reglas ya no imprime los dos campos.
        text.Split("Un premio por persona:").Length.ShouldBe(3);
    }

    /// <summary>
    /// Una noche con todo, completada, y el texto de su acta versión 1. Devuelve el número del acta y
    /// su texto.
    /// </summary>
    private async Task<(string Number, string Text)> RecordOfAFullEveningAsync()
    {
        var (_, record) = await CompleteAFullEveningAsync();
        return (NotarialRecordContent.NumberOf(record.Id), await TextOfRecordAsync(record.NotarialRecords[0].StoredFileId));
    }

    private async Task<string> TextOfRecordAsync(Guid storedFileId) =>
        TextOf(await _client.GetByteArrayAsync($"v1/files/{storedFileId}/content"));

    /// <summary>
    /// Una noche con todo: la marca del evento, dos rondas, una exclusión manual decidida por alguien
    /// que el realm no conoce y una inclusión por alguien que sí, una anulación con nota y la
    /// reactivación de ese ganador, un premio patrocinado sin sortear y uno agregado después de
    /// arrancar. Devuelve el evento y el sorteo ya completado.
    /// </summary>
    private async Task<(Guid EventId, RaffleResponse Completed)> CompleteAFullEveningAsync()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        (await _client.PutAsJsonAsync($"v1/events/{eventId}/raffle/branding", new { companyName = Organizer }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.Attendance.SetDoor(eventId,
            FakeAttendanceSource.Invitee("fabio@agencia.com", "Fabio", "Núñez"),
            FakeAttendanceSource.Invitee("ana@agencia.com", "Ana", "Álvarez"),
            FakeAttendanceSource.Invitee("beto@agencia.com", "Beto", "Báez"),
            FakeAttendanceSource.Invitee("carla@agencia.com", "Carla", "Castro"),
            FakeAttendanceSource.Invitee("dalia@viajes.com.do", "Dalia", "Duarte"),
            FakeAttendanceSource.Invitee("elena@agencia.com", "Elena", "Espinal"));

        var firstRound = await _api.CreateRoundAsync(eventId, "Primera ronda");
        var bolsa = (await _api.CreatePrizesAsync(eventId, firstRound, "Bolsa"))[0].Id;
        await _api.CreatePrizesAsync(eventId, firstRound, "Maleta", sponsorName: SponsorName);
        var finalRound = await _api.CreateRoundAsync(eventId, "Ronda final");
        var crucero = (await _api.CreatePrizesAsync(eventId, finalRound, "Crucero"))[0].Id;

        (await _api.SetOverrideAsync(eventId, "beto@agencia.com", AttendeeOverrideDecision.Excluded, "Pedido del cliente"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var include = new HttpRequestMessage(HttpMethod.Post, OverridesUrl(eventId))
        {
            Content = JsonContent.Create(new SetAttendeeOverrideRequest
            {
                Email = "dalia@viajes.com.do", Decision = AttendeeOverrideDecision.Included, Note = "Lo pidió el director",
            }),
        };
        include.Headers.Add(TestAuthHandler.SubHeader, KnownOperator.ToString());
        (await _client.SendAsync(include)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ShouldBeOkAsync(await _api.StartAsync(eventId));
        var sorpresa = (await _api.CreatePrizesAsync(eventId, finalRound, "Sorpresa"))[0].Id;

        await ShouldBeOkAsync(await _api.DrawAsync(eventId, bolsa));
        await ShouldBeOkAsync(await _api.VoidAsync(eventId, bolsa, PrizeDrawVoidCode.Other, "Es empleado del hotel"));
        var voided = (await _api.GetStoredDrawsAsync(eventId)).Single(d => d.VoidedAt is not null);
        var reinstate = new HttpRequestMessage(HttpMethod.Post, $"{RaffleUrl(eventId)}/attendees/{voided.WinnerAttendeeId}/reinstate");
        reinstate.Headers.Add(TestAuthHandler.SubHeader, KnownOperator.ToString());
        await ShouldBeOkAsync(await _client.SendAsync(reinstate));
        await ShouldBeOkAsync(await _api.DrawAsync(eventId, bolsa));
        await ShouldBeOkAsync(await _api.DrawAsync(eventId, crucero));
        await ShouldBeOkAsync(await _api.DrawAsync(eventId, sorpresa));

        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());
        var complete = await _api.CompleteAsync(eventId);
        await ShouldBeOkAsync(complete);
        return (eventId, (await complete.Content.ReadFromJsonAsync<RaffleResponse>())!);
    }

    /// <summary>
    /// Sortea dos premios entre tres personas en la noche del cambio de horario de Chicago y devuelve el
    /// sorteo ya completado.
    /// </summary>
    /// <remarks>
    /// El reloj de la aplicación queda fijado en esa noche mientras dura el sorteo y se suelta al final,
    /// porque lo comparten los demás tests de la clase.
    /// </remarks>
    private async Task<RaffleResponse> CompleteAnEveningOnTheNightOfTheChangeAsync(Guid eventId)
    {
        _factory.Attendance.SetDoor(eventId, FakeAttendanceSource.Invitees(3));
        var prizes = await _api.CreatePrizesAsync(eventId, await _api.CreateRoundAsync(eventId), quantity: 2);

        _factory.Clock.Pin(NightOfTheChange);
        try
        {
            await ShouldBeOkAsync(await _api.StartAsync(eventId));
            foreach (var prize in prizes)
            {
                _factory.Clock.Advance(TimeSpan.FromMinutes(20));
                await ShouldBeOkAsync(await _api.DrawAsync(eventId, prize.Id));
            }

            _factory.Clock.Advance(TimeSpan.FromMinutes(10));
            await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());
            var complete = await _api.CompleteAsync(eventId);
            await ShouldBeOkAsync(complete);
            return (await complete.Content.ReadFromJsonAsync<RaffleResponse>())!;
        }
        finally
        {
            _factory.Clock.Release();
        }
    }

    /// <summary>Edita la ciudad con la que se creó el evento, que el PATCH reemplaza entera.</summary>
    private async Task PatchTheCityAsync(Guid eventId, string name, string country, string timeZone)
    {
        var detail = (await _client.GetFromJsonAsync<EventDetailResponse>($"v1/events/{eventId}"))!;
        var city = detail.Cities.Single(c => c.DisplayOrder == 1);

        var patch = await _client.PatchAsJsonAsync($"v1/events/{eventId}/cities/{city.Id}", new UpdateCityRequest
        {
            Name = name, Country = country, TimeZone = timeZone, DisplayOrder = city.DisplayOrder,
        });
        patch.StatusCode.ShouldBe(HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
    }

    private static async Task ShouldBeOkAsync(HttpResponseMessage response) =>
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

    /// <summary>
    /// Cada marca aparece, y después de la anterior. Se comparan sin espacios: un corte de línea parte
    /// palabras donde quiere —un id se corta en un guion— y eso no cambia lo que el acta dice.
    /// </summary>
    private static void ShouldAppearInOrder(string text, params string[] markers)
    {
        var squeezed = WithoutWhitespace(text);
        var from = 0;
        foreach (var marker in markers.Select(WithoutWhitespace))
        {
            var at = squeezed.IndexOf(marker, from, StringComparison.Ordinal);
            at.ShouldBeGreaterThanOrEqualTo(0,
                $"Falta, o está fuera de orden: «{marker}». Lo que sigue: «{squeezed[from..Math.Min(squeezed.Length, from + 300)]}»");
            from = at + marker.Length;
        }
    }

    private static string WithoutWhitespace(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));

    /// <summary>El texto de todas las páginas, palabra por palabra separadas por un espacio.</summary>
    private static string TextOf(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join(" ", document.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text));
    }
}
