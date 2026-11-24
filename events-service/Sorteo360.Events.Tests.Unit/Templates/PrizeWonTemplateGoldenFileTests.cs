using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Templates;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Templates;

/// <summary>
/// Archivo dorado del correo al ganador, en sus dos idiomas. Lo mismo que
/// <see cref="QuoteTemplateGoldenFileTests"/>: cambiar una palabra de la copia obliga a actualizar un
/// archivo que alguien mira.
/// </summary>
public sealed class PrizeWonTemplateGoldenFileTests
{
    /// <summary>
    /// La bolsa de datos fija. Las instrucciones llevan saltos de línea, un <c>&amp;</c> y un
    /// <c>&lt;</c>: las escribe el operador en un campo libre, y el escape es parte de lo que el
    /// archivo dorado tiene que mostrar.
    /// </summary>
    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
    {
        ["nombre"] = "María Rodríguez",
        ["premio"] = "Estadía de dos noches en Punta Cana",
        ["evento"] = "Feria de Turismo & Cultura 2026",
        ["fechas"] = "jueves, 12 de marzo de 2026 – sábado, 14 de marzo de 2026",
        ["codigo"] = "RD-7K3M-9QXP",
        ["instrucciones"] = "Stand de MITUR, pabellón 4.\nHasta el viernes a las 18:00 <hora local> & sin excepción.",
        ["organizador"] = "Sorteo360",
    };

    [Fact]
    public void SpanishBody_ReturnsTheApprovedCopy_WhenRendered()
    {
        TemplateDialect.Substitute(Row(AllowedLanguage.Espanol).BodyHtml, Values, escapeHtml: true)
            .ShouldBe(TemplateDialect.ReadGolden("events.prize.won.es.html"));
    }

    [Fact]
    public void SpanishSubject_ReturnsTheApprovedCopy_WhenRendered()
    {
        TemplateDialect.Substitute(Row(AllowedLanguage.Espanol).Subject, Values, escapeHtml: false)
            .ShouldBe("¡Felicidades! Usted ganó Estadía de dos noches en Punta Cana en Feria de Turismo & Cultura 2026");
    }

    [Fact]
    public void EnglishBody_ReturnsTheApprovedCopy_WhenRendered()
    {
        TemplateDialect.Substitute(Row(AllowedLanguage.Ingles).BodyHtml, Values, escapeHtml: true)
            .ShouldBe(TemplateDialect.ReadGolden("events.prize.won.en.html"));
    }

    [Fact]
    public void EnglishSubject_ReturnsTheApprovedCopy_WhenRendered()
    {
        TemplateDialect.Substitute(Row(AllowedLanguage.Ingles).Subject, Values, escapeHtml: false)
            .ShouldBe("Congratulations! You won Estadía de dos noches en Punta Cana at Feria de Turismo & Cultura 2026");
    }

    private static NotificationTemplate Row(string language) =>
        PrizeWonTemplateSeed.Rows.Single(r => r.Language == language);
}
