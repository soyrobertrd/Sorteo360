using System.Text;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.NotarialRecords;
using Shouldly;
using UglyToad.PdfPig;

namespace Sorteo360.Events.Tests.Unit.NotarialRecords;

/// <summary>
/// El acta como PDF. Lo que se afirma es el texto que se extrae de vuelta, no los bytes: el orden y
/// las ausencias sobreviven a un cambio de métricas de la fuente, y una comparación de bytes no.
/// </summary>
public sealed class MigraDocNotarialRecordRendererTests
{
    private static readonly NotarialRecordContent Content = NotarialRecordSamples.Content();

    [Fact]
    public void Render_ReturnsAPdf_WhenGivenAContent()
    {
        var bytes = new MigraDocNotarialRecordRenderer().Render(Content);

        Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    public void Render_ReturnsTheNumberVersionAndEventWithTheirAccents_WhenRead()
    {
        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(Content));

        text.ShouldContain("Acta N.º 3F9A1C02");
        text.ShouldContain("Versión 1");
        text.ShouldContain("Roadshow Bávaro");
    }

    [Fact]
    public void Render_ReturnsTheOrganizerName_WhenProvidedOnTheEvent()
    {
        var withOrganizer = Content with
        {
            Event = Content.Event with { OrganizerName = "Banreservas" },
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(withOrganizer));

        text.ShouldContain("Organizador: Banreservas");
    }

    [Fact]
    public void Render_ReturnsNoReplacesLine_WhenTheVersionIsTheFirst()
    {
        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(Content));

        text.ShouldNotContain("Reemplaza");
    }

    [Fact]
    public void Render_ReturnsTheReplacesLineInSantoDomingoBeforeTheEvent_WhenReissued()
    {
        // 02:15 UTC del 8 son las 22:15 del 7 en Santo Domingo.
        var reissued = Content with
        {
            Version = 2,
            Replaces = new NotarialRecordReplaces(1, new DateTimeOffset(2026, 10, 8, 2, 15, 0, TimeSpan.Zero)),
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(reissued));

        var line = "Versión 2. Reemplaza la versión 1, generada el 07/10/2026 22:15 (hora de Punta Cana, UTC\u221204:00).";
        text.ShouldContain(line);
        text.IndexOf(line, StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("Roadshow Bávaro", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_ReturnsTheTimesInTheCitysZoneAndSaysSo_WhenTheInstantIsPastMidnightInUtc()
    {
        // 01:40 UTC del 7 son las 21:40 del 6 en Punta Cana, que es lo que el notario ve en su reloj.
        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(Content));

        text.ShouldContain("6 de octubre de 2026, 21:40 (hora de Punta Cana, UTC\u221204:00)");
        text.ShouldNotContain("01:40");
    }

    /// <summary>
    /// Sin la diferencia con UTC al lado de cada hora, una noche que cruza un cambio de horario
    /// parecería poner el segundo sorteo antes que el primero.
    /// </summary>
    /// <remarks>
    /// Chicago atrasa la hora a las 02:00 del 2 de noviembre de 2025: las 01:50 de antes y las 01:10 de
    /// después son, en ese orden, dos momentos con veinte minutos entre ellos.
    /// </remarks>
    [Fact]
    public void Render_ReturnsEachTimeWithItsOwnOffset_WhenTheEveningCrossesADaylightSavingChange()
    {
        var startedAt = new DateTimeOffset(2025, 11, 2, 6, 30, 0, TimeSpan.Zero);
        var content = InChicago() with
        {
            StartedAt = startedAt,
            CompletedAt = startedAt.AddMinutes(50),
            DrawLog =
            [
                new NotarialRecordDrawLine(startedAt.AddMinutes(20), 1, "Ronda", "Bolsa", "Ana Álvarez", null, 3, true, false, null, null, null),
                new NotarialRecordDrawLine(startedAt.AddMinutes(40), 1, "Ronda", "Maleta", "Beto Báez", null, 2, true, false, null, null, null),
            ],
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(content));

        text.ShouldContain("2 de noviembre de 2025, 01:30 (hora de Chicago, UTC\u221205:00)");
        // En la tabla la hora ocupa dos renglones de su celda, y el texto extra\u00eddo intercala entre
        // ellos el resto de la fila: se afirma el orden, no la contig\u00fcidad.
        text.ShouldMatch(
            @"(?s)02/11/2025 01:50:00 .*?\(hora de Chicago, UTC\u221205:00\).*?" +
            @"02/11/2025 01:10:00 .*?\(hora de Chicago, UTC\u221206:00\)");
        text.ShouldContain("2 de noviembre de 2025, 01:20 (hora de Chicago, UTC\u221206:00)");
    }

    [Fact]
    public void Render_ReturnsAPlusZeroOffset_WhenTheCityIsOnUtc()
    {
        var content = Content with
        {
            Event = Content.Event with { Places = [new NotarialRecordPlace("Londres", "United Kingdom", CityTimeZone.Create("Europe/London").Value)] },
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(content));

        text.ShouldContain("7 de octubre de 2026, 02:40 (hora de Londres, UTC+01:00)");
        TextOf(new MigraDocNotarialRecordRenderer().Render(content with
        {
            StartedAt = new DateTimeOffset(2026, 1, 15, 20, 0, 0, TimeSpan.Zero),
        })).ShouldContain("15 de enero de 2026, 20:00 (hora de Londres, UTC+00:00)");
    }

    [Fact]
    public void Render_ClosesTheBodyWithANoteNamingTheCityAndCountry_WhenRead()
    {
        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(InChicago()));

        var note = "Las horas están en la hora local de Chicago, United States; cada una indica su diferencia con UTC.";
        text.ShouldContain(note);
        text.IndexOf(note, StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("Anexo:", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_ReturnsTheFirstPlacesClock_WhenTheEventStillHasTwoCities()
    {
        // Un evento tiene una ciudad, pero si los datos traen dos el acta no se niega: toma la hora
        // de la primera en orden de pantalla y la nombra.
        var content = Content with
        {
            Event = Content.Event with
            {
                Places =
                [
                    new NotarialRecordPlace("Chicago", "United States", CityTimeZone.Create("America/Chicago").Value),
                    new NotarialRecordPlace("Punta Cana", "República Dominicana", CityTimeZone.Create("America/Santo_Domingo").Value),
                ],
            },
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(content));

        text.ShouldContain("Lugar: Chicago, United States; Punta Cana, República Dominicana");
        text.ShouldContain("6 de octubre de 2026, 20:40 (hora de Chicago, UTC\u221205:00)");
        text.ShouldNotContain("hora de Punta Cana");
    }

    [Fact]
    public void Render_ReturnsSeveralPagesNumberedOnEveryOne_WhenTheAnnexHas400People()
    {
        var bytes = new MigraDocNotarialRecordRenderer().Render(NotarialRecordSamples.Content(participants: 400));

        using var document = PdfDocument.Open(bytes);
        var pages = document.NumberOfPages;
        pages.ShouldBeGreaterThan(1);
        bytes.Length.ShouldBeLessThan(2 * 1024 * 1024);
        foreach (var page in document.GetPages())
            string.Join(" ", page.GetWords().Select(w => w.Text)).ShouldContain($"Página {page.Number} de {pages}");

        var text = TextOf(bytes);
        text.ShouldContain("Persona Número 400");
        text.ShouldContain("Agencia Ñandú 400");
    }

    [Fact]
    public void Render_ReturnsTheVoidWithItsReasonAndNote_WhenADrawWasVoided()
    {
        var at = NotarialRecordSamples.StartedAt.AddMinutes(5);
        var content = Content with
        {
            DrawLog =
            [
                new NotarialRecordDrawLine(at, 1, "Primera ronda", "Bolsa", "Ana Álvarez", "Viajes Caribe", 40, true, false,
                    PrizeDrawVoidCode.Other, at.AddMinutes(1), "Es empleado del hotel"),
            ],
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(content));

        text.ShouldContain("Ana Álvarez (Viajes Caribe)");
        text.ShouldContain("Anulado: otro motivo. Nota: Es empleado del hotel");
    }

    [Fact]
    public void Render_ReturnsTheTwoRulesOnceAndNoneOnTheDrawLines_WhenTheRulesNeverChanged()
    {
        var at = NotarialRecordSamples.StartedAt;
        var content = Content with
        {
            DrawLog = [new NotarialRecordDrawLine(at, 1, "Ronda", "Bolsa", "Ana Álvarez", null, 3, true, false, null, null, null)],
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(content));

        text.ShouldContain("Un premio por persona: sí");
        text.ShouldContain("Un premio por empresa: no");
        text.ShouldNotContain("Las reglas cambiaron");
        text.ShouldNotContain("Un premio por persona: sí. Un premio por empresa: no.");
    }

    [Fact]
    public void Render_ReturnsTheChangedRulesSentenceAndEachDrawsRules_WhenTheRulesChanged()
    {
        var at = NotarialRecordSamples.StartedAt;
        var content = Content with
        {
            RulesChanged = true,
            DrawLog =
            [
                new NotarialRecordDrawLine(at, 1, "Ronda", "Bolsa", "Ana Álvarez", null, 3, true, false,
                    PrizeDrawVoidCode.Declined, at.AddMinutes(1), null),
                new NotarialRecordDrawLine(at.AddMinutes(2), 1, "Ronda", "Bolsa", "Beto Báez", null, 3, false, true, null, null, null),
            ],
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(content));

        var sentence = text.IndexOf("Las reglas cambiaron durante el sorteo; cada sorteo indica las suyas.", StringComparison.Ordinal);
        var first = text.IndexOf("Un premio por persona: sí. Un premio por empresa: no.", StringComparison.Ordinal);
        var second = text.IndexOf("Un premio por persona: no. Un premio por empresa: sí.", StringComparison.Ordinal);
        sentence.ShouldBeGreaterThanOrEqualTo(0);
        first.ShouldBeGreaterThan(text.IndexOf("Anulado: el ganador rechazó el premio", StringComparison.Ordinal));
        second.ShouldBeGreaterThan(text.IndexOf("Beto Báez", StringComparison.Ordinal));
        first.ShouldBeLessThan(second);
        // Solo las dos líneas del registro: la sección de reglas ya no imprime los dos campos.
        text.Split("Un premio por persona:").Length.ShouldBe(3);
    }

    public static TheoryData<string> ExclusionReasons => [.. ExclusionReason.InEvaluationOrder];

    [Theory]
    [MemberData(nameof(ExclusionReasons))]
    public void Render_ReturnsAPdf_WhenAnyExclusionReasonIsCountedAndOverruled(string reason)
    {
        // Cada motivo tiene su redacción: uno sin ella haría fallar complete, no el acta.
        var content = Content with
        {
            Arithmetic = new NotarialRecordArithmetic(2, 1, 1, [new NotarialRecordExclusion(reason, 1)]),
            ManualDecisions = reason == ExclusionReason.ManuallyExcluded
                ? []
                : [new NotarialRecordDecision("Ana Álvarez", null, AttendeeOverrideDecision.Included, reason, false,
                    Guid.NewGuid(), "Luis Gómez", NotarialRecordSamples.StartedAt)],
        };

        Should.NotThrow(() => new MigraDocNotarialRecordRenderer().Render(content));
    }

    public static TheoryData<string> VoidCodes => [.. PrizeDrawVoidCode.All];

    [Theory]
    [MemberData(nameof(VoidCodes))]
    public void Render_ReturnsAPdf_WhenADrawWasVoidedForAnyReason(string code)
    {
        var at = NotarialRecordSamples.StartedAt;
        var content = Content with
        {
            DrawLog = [new NotarialRecordDrawLine(at, 1, "Ronda", "Bolsa", "Ana Álvarez", null, 3, true, false, code, at, null)],
        };

        Should.NotThrow(() => new MigraDocNotarialRecordRenderer().Render(content));
    }

    [Fact]
    public void Render_ReturnsTheInclusionAsWithoutEffect_WhenARuleNobodyOverridesBeatIt()
    {
        var content = Content with
        {
            ManualDecisions =
            [
                new NotarialRecordDecision("Ana Álvarez", null, AttendeeOverrideDecision.Included, ExclusionReason.WalkIn,
                    TookEffect: false, Guid.NewGuid(), "Luis Gómez", NotarialRecordSamples.StartedAt),
            ],
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(content));

        text.ShouldContain("Sin efecto: la regla «registro sin invitación» no admite excepciones.");
        text.ShouldNotContain("Regla exceptuada");
    }

    [Fact]
    public void Render_ReturnsTheDrawDateWithItsTime_WhenTheEveningCrossesMidnight()
    {
        // 03:59 UTC del 7 son las 23:59 del 6, y 04:01 UTC es la 00:01 del 7 en Santo Domingo.
        var before = new DateTimeOffset(2026, 10, 7, 3, 59, 0, TimeSpan.Zero);
        var content = Content with
        {
            DrawLog =
            [
                new NotarialRecordDrawLine(before, 1, "Ronda", "Bolsa", "Ana Álvarez", null, 3, true, false, null, null, null),
                new NotarialRecordDrawLine(before.AddMinutes(2), 1, "Ronda", "Maleta", "Beto Báez", null, 2, true, false, null, null, null),
            ],
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(content));

        text.ShouldContain("06/10/2026 23:59:00");
        text.ShouldContain("07/10/2026 00:01:00");
    }

    [Fact]
    public void Render_PrintsReinstatements_WhenContentIncludesReinstatedAttendees()
    {
        var content = Content with
        {
            Reinstatements =
            [
                new NotarialRecordReinstatement(
                    "Ana Álvarez",
                    "Viajes Caribe",
                    Guid.NewGuid(),
                    "Carlos Operador",
                    NotarialRecordSamples.StartedAt.AddMinutes(15)),
            ],
        };

        var text = TextOf(new MigraDocNotarialRecordRenderer().Render(content));

        text.ShouldContain("Reactivaciones tras anulación");
        text.ShouldContain("Ana Álvarez (Viajes Caribe): reactivación en el sorteo tras anulación. Decidió Carlos Operador");
    }

    [Fact]
    public void Render_ReturnsTheSameText_WhenCalledTwiceInOneProcess()
    {
        // La fuente se registra una vez por proceso; un segundo renderizador no la vuelve a asignar.
        var first = TextOf(new MigraDocNotarialRecordRenderer().Render(Content));
        var second = TextOf(new MigraDocNotarialRecordRenderer().Render(Content));

        second.ShouldBe(first);
    }

    [Fact]
    public void ResolveTypeface_ReturnsNothing_WhenItalicOrAnotherFamilyIsAsked()
    {
        // Sin respaldo: una cursiva o una familia que no viaja en el ensamblado falla en voz alta.
        var resolver = new NotoSerifFontResolver();

        resolver.ResolveTypeface("Noto Serif", bold: false, italic: true).ShouldBeNull();
        resolver.ResolveTypeface("Arial", bold: false, italic: false).ShouldBeNull();
        resolver.ResolveTypeface("Noto Serif", bold: true, italic: false).ShouldNotBeNull();
    }

    private static NotarialRecordContent InChicago() => Content with
    {
        Event = Content.Event with { Places = [new NotarialRecordPlace("Chicago", "United States", CityTimeZone.Create("America/Chicago").Value)] },
    };

    /// <summary>El texto de todas las páginas, en orden, una línea por palabra extraída.</summary>
    internal static string TextOf(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join("\n", document.GetPages().Select(page =>
            string.Join(" ", page.GetWords().Select(w => w.Text))));
    }
}
