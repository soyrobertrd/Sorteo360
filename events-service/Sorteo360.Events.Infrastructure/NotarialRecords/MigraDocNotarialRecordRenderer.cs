using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Infrastructure.NotarialRecords;

/// <summary>
/// El acta notarial en PDF, con MigraDoc (ADR-0024), en el orden de spec 03 §4.2. Toda la redacción
/// en español del documento vive acá, junto a la maquetación: el dominio entrega hechos y esta clase
/// decide cómo se dicen.
/// <para>
/// <b>Las horas van en la hora local de la primera ciudad del evento y lo dicen.</b> Un notario las
/// compara con su reloj, y en UTC las 21:40 se leen como la 01:40 del día siguiente. Cada hora lleva
/// además su diferencia con UTC, calculada para ese instante: una noche que cruza un cambio de horario
/// tiene dos, y sin ella dos sorteos seguidos podrían leerse en el orden inverso.
/// </para>
/// </summary>
public sealed class MigraDocNotarialRecordRenderer : INotarialRecordRenderer
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-DO");

    /// <summary>Por qué quedó fuera cada uno, en las palabras del acta (spec 01 §6).</summary>
    private static readonly IReadOnlyDictionary<string, string> ExclusionLabels = new Dictionary<string, string>
    {
        [ExclusionReason.WalkIn] = "llegaron sin invitación",
        [ExclusionReason.ContactUnresolved] = "sin contacto en el CRM",
        [ExclusionReason.Suspended] = "suspendidos en el CRM",
        [ExclusionReason.DoNotInvite] = "marcados en el CRM para no invitar",
        [ExclusionReason.ManuallyExcluded] = "excluidos por decisión manual",
        [ExclusionReason.NotAwardsMember] = "fuera de la lista de DO Travel Awards",
        [ExclusionReason.DominicanDomain] = "correo con dominio .do",
    };

    /// <summary>
    /// La regla que una decisión manual tocó, dicha de una persona y sin género: el acta no sabe el de
    /// nadie.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> RuleLabels = new Dictionary<string, string>
    {
        [ExclusionReason.WalkIn] = "registro sin invitación",
        [ExclusionReason.ContactUnresolved] = "sin contacto en el CRM",
        [ExclusionReason.Suspended] = "contacto suspendido en el CRM",
        [ExclusionReason.DoNotInvite] = "contacto marcado en el CRM para no invitar",
        [ExclusionReason.NotAwardsMember] = "fuera de la lista de DO Travel Awards",
        [ExclusionReason.DominicanDomain] = "correo con dominio .do",
    };

    private static readonly IReadOnlyDictionary<string, string> VoidLabels = new Dictionary<string, string>
    {
        [PrizeDrawVoidCode.Absent] = "el ganador no estaba presente",
        [PrizeDrawVoidCode.Declined] = "el ganador rechazó el premio",
        [PrizeDrawVoidCode.Ineligible] = "el ganador no podía participar",
        [PrizeDrawVoidCode.Other] = "otro motivo",
    };

    public MigraDocNotarialRecordRenderer() => NotoSerifFontResolver.EnsureRegistered();

    public byte[] Render(NotarialRecordContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var document = CreateDocument();
        var section = AddSection(document, content);

        var clock = RecordClock.Of(content.Event);

        AddHeader(section, content, clock);
        AddEvent(section, content, clock);
        AddRules(section, content);
        AddMethod(section);
        AddArithmetic(section, content.Arithmetic);
        AddDecisions(section, content.ManualDecisions, clock);
        AddReinstatements(section, content.Reinstatements, clock);
        AddResults(section, content);
        AddDrawLog(section, content.DrawLog, content.RulesChanged, clock);
        AddTimeNote(section, clock);
        AddAuthorities(section, content.Authorities);
        // El anexo ya empieza en página nueva; en su propia sección MigraDoc no repagina el cuerpo con
        // él (25.000 filas, A31). La numeración de páginas sigue de corrido.
        AddAnnex(AddSection(document, content), content.Annex);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static Document CreateDocument()
    {
        var document = new Document();
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = NotoSerifFontResolver.FamilyName;
        normal.Font.Size = 10;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);

        var heading = document.Styles[StyleNames.Heading1]!;
        heading.Font.Size = 11.5;
        heading.Font.Bold = true;
        heading.ParagraphFormat.SpaceBefore = Unit.FromPoint(12);
        heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        heading.ParagraphFormat.KeepWithNext = true;

        var subheading = document.Styles[StyleNames.Heading2]!;
        subheading.Font.Size = 10.5;
        subheading.Font.Bold = true;
        subheading.ParagraphFormat.SpaceBefore = Unit.FromPoint(8);
        subheading.ParagraphFormat.KeepWithNext = true;

        return document;
    }

    // ----- Punto 1 y pie: número, versión, página -----

    /// <summary>Una sección carta con los márgenes y el pie del acta.</summary>
    private static Section AddSection(Document document, NotarialRecordContent content)
    {
        var section = document.AddSection();
        section.PageSetup = document.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.Letter;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2.2);
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2.2);
        AddFooter(section, content);
        return section;
    }

    private static void AddFooter(Section section, NotarialRecordContent content)
    {
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 8.5;
        footer.AddText($"Acta N.º {content.RecordNumber} · Versión {content.Version} · Página ");
        footer.AddPageField();
        footer.AddText(" de ");
        footer.AddNumPagesField();
    }

    private static void AddHeader(Section section, NotarialRecordContent content, RecordClock clock)
    {
        var title = section.AddParagraph("ACTA NOTARIAL DE SORTEO");
        title.Format.Font.Bold = true;
        title.Format.Font.Size = 15;
        title.Format.Alignment = ParagraphAlignment.Center;

        var number = section.AddParagraph($"Acta N.º {content.RecordNumber} · Versión {content.Version}");
        number.Format.Alignment = ParagraphAlignment.Center;
        number.Format.SpaceAfter = Unit.FromPoint(10);

        // Una reemisión dice a cuál reemplaza y cuándo se generó esa; el acta no dice cuándo se generó
        // ella misma, porque sus horas son las del sorteo (spec 03 §4.5).
        if (content.Replaces is { } replaces)
        {
            var replacing = section.AddParagraph(
                $"Versión {content.Version}. Reemplaza la versión {replaces.Version}, generada el " +
                $"{clock.Format(replaces.GeneratedAt, "dd/MM/yyyy HH:mm")}.");
            replacing.Format.Alignment = ParagraphAlignment.Center;
            replacing.Format.SpaceAfter = Unit.FromPoint(10);
        }
    }

    // ----- Puntos 2 y 3: el evento y las horas -----

    private static void AddEvent(Section section, NotarialRecordContent content, RecordClock clock)
    {
        section.AddParagraph("Evento", StyleNames.Heading1);
        var @event = content.Event;
        AddField(section, "Evento", @event.Name);
        if (!string.IsNullOrWhiteSpace(@event.OrganizerName))
            AddField(section, "Organizador", @event.OrganizerName);
        AddField(section, "Fechas", @event.StartDate == @event.EndDate
            ? Date(@event.StartDate)
            : $"del {Date(@event.StartDate)} al {Date(@event.EndDate)}");

        // Un evento tiene una ciudad; si los datos traen más, van todas y ninguna se calla.
        AddField(section, "Lugar", string.Join("; ", @event.Places.Select(p => $"{p.City}, {p.Country}")));

        AddField(section, "Inicio del sorteo y cierre de la lista de participantes", clock.Moment(content.StartedAt));
        AddField(section, "Cierre del sorteo", clock.Moment(content.CompletedAt));
    }

    // ----- Puntos 4 y 5: las reglas y las advertencias -----

    private static void AddRules(Section section, NotarialRecordContent content)
    {
        section.AddParagraph("Reglas y advertencias", StyleNames.Heading1);
        if (content.RulesChanged)
        {
            // Cada línea del registro cronológico dice las suyas (spec 03 §4.2 punto 4).
            section.AddParagraph("Las reglas cambiaron durante el sorteo; cada sorteo indica las suyas.");
        }
        else
        {
            AddField(section, "Un premio por persona", YesNo(content.OnePrizePerPerson));
            AddField(section, "Un premio por empresa", YesNo(content.OnePrizePerCompany));
        }

        if (content.LinkedRaffleNames.Count > 0)
        {
            AddField(section, "Sorteos vinculados", string.Join(", ", content.LinkedRaffleNames));
            section.AddParagraph(
                "Quien tenía un premio vigente en un sorteo vinculado quedó fuera del bombo de este sorteo.");
        }

        section.AddParagraph(content.AwardsListApplied
            ? "Al cerrar la lista de participantes se aplicó la lista de DO Travel Awards en vigor."
            : "Advertencia: al cerrar la lista de participantes no había una lista de DO Travel Awards en vigor, " +
              "así que ese requisito no se aplicó.");
    }

    // ----- Punto 6: cómo se eligió -----

    private static void AddMethod(Section section)
    {
        section.AddParagraph("Método de selección", StyleNames.Heading1);
        section.AddParagraph(
            "Cada ganador se eligió al azar entre las personas habilitadas para ese premio, todas con la misma " +
            "probabilidad, usando el generador de números aleatorios criptográfico del sistema operativo. " +
            "El grupo de personas habilitadas se volvió a calcular antes de sortear cada premio.");
    }

    // ----- Punto 7: la aritmética -----

    private static void AddArithmetic(Section section, NotarialRecordArithmetic arithmetic)
    {
        section.AddParagraph("Participantes", StyleNames.Heading1);
        AddField(section, "Registrados en la puerta", FormatCount(arithmetic.CheckedIn));
        AddField(section, "En el sorteo", FormatCount(arithmetic.Qualified));
        AddField(section, "Fuera del sorteo", FormatCount(arithmetic.NotQualified));
        foreach (var exclusion in arithmetic.ExcludedByReason)
        {
            var line = section.AddParagraph($"{ExclusionLabels[exclusion.Reason]}: {FormatCount(exclusion.Count)}");
            line.Format.LeftIndent = Unit.FromCentimeter(0.8);
        }
    }

    // ----- Punto 8: las decisiones manuales -----

    private static void AddDecisions(Section section, IReadOnlyList<NotarialRecordDecision> decisions, RecordClock clock)
    {
        section.AddParagraph("Decisiones manuales", StyleNames.Heading1);
        if (decisions.Count == 0)
        {
            section.AddParagraph("No hubo decisiones manuales.");
            return;
        }

        foreach (var decision in decisions)
        {
            var direction = decision.Decision == AttendeeOverrideDecision.Included
                ? "inclusión manual en el sorteo"
                : "exclusión manual del sorteo";
            var rule = (decision.OverruledReason, decision.TookEffect) switch
            {
                ({ } reason, true) => $" Regla exceptuada: {RuleLabels[reason]}.",
                ({ } reason, false) => $" Sin efecto: la regla «{RuleLabels[reason]}» no admite excepciones.",
                _ => string.Empty,
            };
            var decider = decision.DecidedByName ?? $"usuario no disponible ({decision.DecidedById})";

            section.AddParagraph(
                $"{Person(decision.Name, decision.Company)}: {direction}.{rule} " +
                $"Decidió {decider}, el {clock.Moment(decision.DecidedAt)}.");
        }
    }

    private static void AddReinstatements(
        Section section,
        IReadOnlyList<NotarialRecordReinstatement> reinstatements,
        RecordClock clock)
    {
        if (reinstatements.Count == 0)
            return;

        section.AddParagraph("Reactivaciones tras anulación", StyleNames.Heading2);
        foreach (var item in reinstatements)
        {
            var actor = item.ReinstatedByName ?? $"usuario no disponible ({item.ReinstatedById})";
            section.AddParagraph(
                $"{Person(item.Name, item.Company)}: reactivación en el sorteo tras anulación. " +
                $"Decidió {actor}, el {clock.Moment(item.ReinstatedAt)}.");
        }
    }

    // ----- Punto 9: los resultados por ronda -----

    private static void AddResults(Section section, NotarialRecordContent content)
    {
        section.AddParagraph("Resultados por ronda", StyleNames.Heading1);
        foreach (var round in content.Rounds)
        {
            section.AddParagraph($"Ronda {round.Number}: {round.Name}", StyleNames.Heading2);
            if (round.Prizes.Count == 0)
                section.AddParagraph("Sin premios.");

            foreach (var prize in round.Prizes)
            {
                var name = prize.Value is null ? prize.Name : $"{prize.Name} ({prize.Value})";
                if (prize.SponsorCompany is not null)
                    name = $"{name}, patrocinado por {prize.SponsorCompany}";
                var added = prize.AddedDuringRaffle ? " (añadido durante el sorteo)" : string.Empty;
                var outcome = prize.WinnerName is null ? "no adjudicado" : Person(prize.WinnerName, prize.WinnerCompany);
                section.AddParagraph($"{name}{added}: {outcome}.");
            }
        }

        section.AddParagraph(
            $"Premios adjudicados: {content.PrizesAwarded}. Premios no adjudicados: {content.PrizesNotAwarded}.");
    }

    // ----- Punto 10: el registro cronológico -----

    private static void AddDrawLog(
        Section section, IReadOnlyList<NotarialRecordDrawLine> log, bool rulesChanged, RecordClock clock)
    {
        section.AddParagraph("Registro cronológico de sorteos", StyleNames.Heading1);
        if (log.Count == 0)
        {
            section.AddParagraph("No se sorteó ningún premio.");
            return;
        }

        section.AddParagraph("El grupo es cuántas personas podían ganar en ese sorteo.");

        Table? table = null;
        for (var i = 0; i < log.Count; i++)
        {
            var line = log[i];
            if (i % RowsPerTable == 0)
                table = AddTable(section, [3.5, 2.9, 3.2, 5.0, 1.6], "Fecha y hora", "Ronda", "Premio", "Ganador", "Grupo");

            var row = table!.AddRow();
            row.Cells[0].AddParagraph(clock.Time(line.DrawnAt));
            row.Cells[1].AddParagraph($"{line.RoundNumber}. {line.RoundName}");
            row.Cells[2].AddParagraph(line.PrizeName);
            row.Cells[3].AddParagraph(Person(line.WinnerName, line.WinnerCompany));
            row.Cells[4].AddParagraph(FormatCount(line.EligiblePoolSize));

            if (line.VoidCode is not null)
            {
                var voided = table.AddRow();
                voided.Cells[0].AddParagraph(clock.Time(line.VoidedAt!.Value));
                voided.Cells[1].MergeRight = 3;
                var note = line.VoidNote is null ? string.Empty : $" Nota: {line.VoidNote}";
                voided.Cells[1].AddParagraph($"Anulado: {VoidLabels[line.VoidCode]}.{note}");
            }

            // Con reglas que cambiaron, cada sorteo, anulado o no, termina con las suyas (spec 03 §4.2 punto 4).
            if (rulesChanged)
            {
                var rules = table.AddRow();
                rules.Cells[1].MergeRight = 3;
                rules.Cells[1].AddParagraph(
                    $"Un premio por persona: {YesNo(line.OnePrizePerPerson)}. " +
                    $"Un premio por empresa: {YesNo(line.OnePrizePerCompany)}.");
            }
        }
    }

    /// <summary>
    /// Agrega la nota que cierra el cuerpo del acta, antes de las firmas, con la ciudad cuyo reloj usan
    /// las horas y su país tal como está guardado.
    /// </summary>
    private static void AddTimeNote(Section section, RecordClock clock)
    {
        var note = section.AddParagraph(
            $"Las horas están en la hora local de {clock.Place.City}, {clock.Place.Country}; " +
            "cada una indica su diferencia con UTC.");
        note.Format.SpaceBefore = Unit.FromPoint(10);
    }

    // ----- Punto 11: las autoridades -----

    private static void AddAuthorities(Section section, RaffleAuthorities authorities)
    {
        section.AddParagraph("Autoridades", StyleNames.Heading1);
        AddSignature(section, "Notario", authorities.NotaryName, authorities.NotaryIdentityDocument);
        AddSignature(section, "Primer testigo", authorities.WitnessOneName, authorities.WitnessOneIdentityDocument);
        AddSignature(section, "Segundo testigo", authorities.WitnessTwoName, authorities.WitnessTwoIdentityDocument);
    }

    private static void AddSignature(Section section, string role, string? name, string? document)
    {
        var block = section.AddParagraph($"{role}: {name}, documento de identidad {document}");
        block.Format.SpaceBefore = Unit.FromPoint(14);
        block.Format.KeepWithNext = true;

        var line = section.AddParagraph("Firma: ________________________________________");
        line.Format.SpaceBefore = Unit.FromPoint(22);
    }

    // ----- Punto 12: el anexo -----

    private static void AddAnnex(Section section, IReadOnlyList<NotarialRecordParticipant> annex)
    {
        section.AddParagraph("Anexo: participantes del sorteo", StyleNames.Heading1);
        section.AddParagraph(
            $"Las {FormatCount(annex.Count)} personas que estaban en el sorteo, por apellido y nombre.");

        Table? table = null;
        for (var i = 0; i < annex.Count; i++)
        {
            var participant = annex[i];
            if (i % RowsPerTable == 0)
                table = AddTable(section, [1.4, 8.2, 6.6], "N.º", "Nombre", "Empresa");

            var row = table!.AddRow();
            row.Cells[0].AddParagraph(participant.Number.ToString(Spanish));
            row.Cells[1].AddParagraph(participant.Name);
            row.Cells[2].AddParagraph(participant.Company ?? string.Empty);
        }
    }

    // ----- Piezas -----

    private static void AddField(Section section, string label, string value)
    {
        var paragraph = section.AddParagraph();
        paragraph.AddFormattedText($"{label}: ", TextFormat.Bold);
        paragraph.AddText(value);
    }

    /// <summary>
    /// Filas por tabla en el registro y el anexo. MigraDoc pagina una tabla en tiempo más que lineal:
    /// un anexo de 25.000 filas en una sola tabla tardaba 40 s y partido en tablas de 100, 9 s (A31).
    /// Cada tabla nueva repite la fila de títulos.
    /// </summary>
    private const int RowsPerTable = 500;

    /// <summary>Una tabla con su fila de títulos, que se repite al cambiar de página.</summary>
    private static Table AddTable(Section section, double[] widthsInCm, params string[] titles)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.4;
        table.Format.Font.Size = 9;
        table.LeftPadding = Unit.FromPoint(3);
        table.RightPadding = Unit.FromPoint(3);
        foreach (var width in widthsInCm)
            table.AddColumn(Unit.FromCentimeter(width));

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        for (var i = 0; i < titles.Length; i++)
            header.Cells[i].AddParagraph(titles[i]);

        return table;
    }

    private static string Person(string name, string? company) =>
        string.IsNullOrWhiteSpace(company) ? name : $"{name} ({company})";

    private static string FormatCount(int count) => count.ToString("N0", Spanish);

    private static string YesNo(bool value) => value ? "sí" : "no";

    private static string Date(DateOnly date) => date.ToString("d 'de' MMMM 'de' yyyy", Spanish);

    /// <summary>
    /// El reloj del acta: la zona de la primera ciudad viva en orden de pantalla, que el rótulo nombra.
    /// </summary>
    /// <remarks>
    /// Un evento tiene una ciudad; si los datos todavía traen más, el acta no se niega y toma la primera.
    /// Siempre hay una, porque la última ciudad de un evento no se puede quitar.
    /// </remarks>
    private sealed class RecordClock
    {
        private RecordClock(NotarialRecordPlace place)
        {
            Place = place;
        }

        /// <summary>La ciudad cuyo reloj usa el acta.</summary>
        public NotarialRecordPlace Place { get; }

        public static RecordClock Of(NotarialRecordEvent @event) => new(@event.Places[0]);

        /// <summary>Fecha y hora, con la zona dicha, para lo que se lee suelto.</summary>
        public string Moment(DateTimeOffset instant) => Format(instant, "d 'de' MMMM 'de' yyyy, HH:mm");

        /// <summary>Día y hora, con la zona dicha, para cada línea del registro cronológico.</summary>
        /// <remarks>Lleva el día porque una noche que pasa de la medianoche tiene sorteos en dos fechas.</remarks>
        public string Time(DateTimeOffset instant) => Format(instant, "dd/MM/yyyy HH:mm:ss");

        /// <summary>
        /// El instante en la hora local con el formato pedido, seguido del rótulo y de la diferencia con
        /// UTC de ese instante, como <c>(hora de Chicago, UTC−05:00)</c>.
        /// </summary>
        /// <remarks>
        /// El signo negativo es el signo menos tipográfico (U+2212), no un guion; una diferencia nula es
        /// <c>UTC+00:00</c>.
        /// </remarks>
        public string Format(DateTimeOffset instant, string format)
        {
            var local = TimeZoneInfo.ConvertTime(instant, Place.TimeZone.Info);
            var sign = local.Offset < TimeSpan.Zero ? "\u2212" : "+";
            return $"{local.DateTime.ToString(format, Spanish)} (hora de {Place.City}, UTC{sign}{local.Offset:hh\\:mm})";
        }
    }
}
