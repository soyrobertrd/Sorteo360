using Sorteo360.Notifications.Core.Rendering;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Rendering;

/// <summary>
/// El motor de sustitución: sintaxis plana, escape del cuerpo, y la parte de texto derivada del
/// HTML. No conoce ninguna clave ni ninguna plantilla, y estos tests lo comprueban sustituyendo
/// marcadores que el servicio nunca vio.
/// </summary>
public sealed class SubstitutionEngineTests
{
    private readonly SubstitutionEngine _renderer = new();

    #region Sustitución

    [Fact]
    public void Render_ReturnsTheSubjectAndBodyReplaced_WhenBothHaveAKey()
    {
        var rendered = Render(
            subject: "Cotización pendiente — {{evento}}",
            body: "<p>Hola {{nombre}}</p>",
            ("evento", "Feria de Madrid 2026"),
            ("nombre", "María"));

        rendered.Subject.ShouldBe("Cotización pendiente — Feria de Madrid 2026");
        rendered.HtmlBody.ShouldBe("<p>Hola María</p>");
    }

    [Fact]
    public void Render_ReturnsEveryOccurrenceReplaced_WhenAKeyRepeats()
    {
        var rendered = Render("x", "<p>{{a}} y {{a}}</p>", ("a", "uno"));

        rendered.HtmlBody.ShouldBe("<p>uno y uno</p>");
    }

    [Fact]
    public void Render_ReturnsTheValueSubstituted_WhenTheKeyIsUndeclared()
    {
        // No hay lista de claves legales, ni tabla de plantillas, ni rama por type: una clave
        // inventada se sustituye igual que cualquier otra.
        var rendered = Render("x", "<p>{{clave_que_nadie_declaro}}</p>", ("clave_que_nadie_declaro", "valor"));

        rendered.HtmlBody.ShouldBe("<p>valor</p>");
    }

    [Fact]
    public void Render_ReturnsTheBodyUnchanged_WhenAValueKeyIsNotInTheBody()
    {
        var rendered = Render("x", "<p>{{a}}</p>", ("a", "uno"), ("sobra", "dos"));

        rendered.HtmlBody.ShouldBe("<p>uno</p>");
        rendered.HtmlBody.ShouldNotContain("dos");
    }

    [Fact]
    public void Render_ReturnsTheValueSubstituted_WhenTheBracesHoldSpaces()
    {
        var rendered = Render("x", "<p>{{ a }}</p>", ("a", "uno"));

        rendered.HtmlBody.ShouldBe("<p>uno</p>");
    }

    #endregion

    #region La sintaxis es plana

    [Theory]
    [InlineData("{{ sin cierre")]
    [InlineData("{{}}")]
    [InlineData("{{#if a}}hola{{/if}}")]
    public void Render_ReturnsTheTextUntouched_WhenItIsNotAPlainMarker(string body)
    {
        // Sin condicionales, sin bucles y sin helpers: lo que no es {{clave}} es texto, y no
        // rompe nada.
        var rendered = Render("x", body, ("a", "uno"));

        rendered.HtmlBody.ShouldBe(body);
    }

    [Fact]
    public void Render_ReturnsAnEscapedValue_WhenTheMarkerHasTripleBraces()
    {
        var rendered = Render("x", "{{{a}}}", ("a", "<b>"));

        // Las llaves de sobra quedan como texto y el valor se escapa igual: no existe {{{crudo}}}.
        rendered.HtmlBody.ShouldBe("{&lt;b&gt;}");
    }

    [Fact]
    public void Render_ReturnsTheMarkerLiteral_WhenTheKeyIsUnresolved()
    {
        // Una petición con marcadores sin valor ya se rechazó con un 400 antes de llegar acá.
        var rendered = Render("Hola {{quien}}", "<p>{{quien}}</p>");

        rendered.Subject.ShouldBe("Hola {{quien}}");
        rendered.HtmlBody.ShouldBe("<p>{{quien}}</p>");
    }

    #endregion

    #region Escape

    [Fact]
    public void Render_ReturnsTheMarkupEscaped_WhenAValueCarriesMarkup()
    {
        var rendered = Render("x", "<p>Hola {{nombre}}</p>", ("nombre", "Juan <script>alert(1)</script>"));

        rendered.HtmlBody.ShouldBe("<p>Hola Juan &lt;script&gt;alert(1)&lt;/script&gt;</p>");
        rendered.HtmlBody.ShouldNotContain("<script>");
    }

    [Fact]
    public void Render_ReturnsTheAmpersandEscaped_WhenAValueCarriesABareAmpersand()
    {
        var rendered = Render("x", "<p>{{empresa}}</p>", ("empresa", "Sol & Luna"));

        rendered.HtmlBody.ShouldBe("<p>Sol &amp; Luna</p>");
    }

    [Fact]
    public void Render_ReturnsAnUnescapedSubject_WhenTheValueCarriesMarkup()
    {
        var rendered = Render("{{empresa}} cotizó", "<p>x</p>", ("empresa", "Sol & Luna"));

        rendered.Subject.ShouldBe("Sol & Luna cotizó");
    }

    [Fact]
    public void Render_ReturnsASingleLineSubject_WhenTheValueCarriesLineBreaks()
    {
        var rendered = Render("Aviso: {{evento}}", "<p>x</p>", ("evento", "Feria\r\nBcc: otro@ejemplo.do"));

        rendered.Subject.ShouldBe("Aviso: Feria  Bcc: otro@ejemplo.do");
    }

    [Fact]
    public void Render_ReturnsTheCallerMarkupUntouched_WhenTheBodyCarriesMarkup()
    {
        var rendered = Render("x", """<a href="https://ejemplo.do">Ver</a>""");

        rendered.HtmlBody.ShouldBe("""<a href="https://ejemplo.do">Ver</a>""");
    }

    #endregion

    #region La parte de texto

    [Fact]
    public void Render_ReturnsATextPartWithNoTags_WhenTheBodyIsHtml()
    {
        var rendered = Render(
            "x",
            "<html><body><h1>Cotización</h1><p>Hola {{nombre}}, hay <b>3</b> candidatos.</p></body></html>",
            ("nombre", "María"));

        rendered.TextBody.ShouldNotContain("<");
        rendered.TextBody.ShouldNotContain(">");
        rendered.TextBody.ShouldContain("Cotización");
        rendered.TextBody.ShouldContain("Hola María, hay 3 candidatos.");
    }

    [Fact]
    public void Render_ReturnsATextPartDistinctFromTheHtml_WhenTheBodyIsHtml()
    {
        var rendered = Render("x", "<p>Hola {{nombre}}</p>", ("nombre", "María"));

        rendered.TextBody.ShouldBe("Hola María");
        rendered.TextBody.ShouldNotBe(rendered.HtmlBody);
    }

    [Fact]
    public void Render_ReturnsTheTextPartUnescaped_WhenTheHtmlWasEscaped()
    {
        var rendered = Render("x", "<p>{{empresa}}</p>", ("empresa", "Sol & Luna"));

        rendered.HtmlBody.ShouldContain("&amp;");
        rendered.TextBody.ShouldBe("Sol & Luna");
    }

    [Fact]
    public void Render_ReturnsATextPartWithNoScriptOrStyle_WhenTheBodyCarriesThem()
    {
        var rendered = Render(
            "x",
            "<html><head><title>No copia</title><style>p { color: red }</style></head><body><p>Copia</p><script>alert(1)</script></body></html>");

        rendered.TextBody.ShouldBe("Copia");
    }

    [Fact]
    public void Render_ReturnsBlocksSeparatedByLineBreaks_WhenTheBodyHasBlocks()
    {
        var rendered = Render("x", "<p>Primera</p><p>Segunda</p><br><p>Tercera</p>");

        rendered.TextBody.ShouldBe("Primera\nSegunda\nTercera");
    }

    [Fact]
    public void Render_ReturnsATextPartWithoutIndentation_WhenTheBodyIsIndented()
    {
        var rendered = Render("x", """
            <html>
              <body>
                <p>
                  Una sola línea
                </p>
              </body>
            </html>
            """);

        rendered.TextBody.ShouldBe("Una sola línea");
    }

    #endregion

    #region Guards

    [Fact]
    public void Render_ThrowsArgumentNullException_WhenAnArgumentIsNull()
    {
        Should.Throw<ArgumentNullException>(() => _renderer.Render(null!, "x", Values()));
        Should.Throw<ArgumentNullException>(() => _renderer.Render("x", null!, Values()));
        Should.Throw<ArgumentNullException>(() => _renderer.Render("x", "y", null!));
    }

    #endregion

    private RenderedEmail Render(string subject, string body, params (string Key, string Value)[] values) =>
        _renderer.Render(subject, body, Values(values));

    private static Dictionary<string, string> Values(params (string Key, string Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
}
