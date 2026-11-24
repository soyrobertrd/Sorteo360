using Sorteo360.Convocatorias.Core.Rendering;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Rendering;

/// <summary>
/// El motor de sustitución: plano, literal y con escape. Sin condicionales, sin bucles, sin
/// helpers y sin escape crudo, que es contrato y no detalle de implementación.
/// </summary>
public sealed class SubstitutionEngineTests
{
    private readonly SubstitutionEngine _engine = new();

    private static Dictionary<string, string> Values(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

    #region Sustitución

    [Fact]
    public void Render_ReturnsEverySupportedKeyReplaced_WhenEachHasAValue()
    {
        var values = Values(
            ("nombre", "Juan"),
            ("evento", "Roadshow Madrid 2026"),
            ("lugar", "Madrid"),
            ("fecha", "12 de junio"),
            ("enlace_registro", "https://app.example/r#t=abc"),
            ("qr", "https://app.example/qr/abc.png"),
            ("enlace_baja", "https://app.example/b#u=xyz"),
            ("referencia", "REF-2026-00427"),
            ("enlace_entrada", "https://app.example/publico/entrada/abc"),
            ("asunto", "Novedades del roadshow"),
            ("mensaje", "Gracias por su interés."));

        var body = string.Concat(SupportedPlaceholder.All.Keys.Select(key => $"[{{{{{key}}}}}]"));

        var rendered = _engine.Render(string.Empty, body, values);

        rendered.BodyHtml.ShouldNotContain("{{");
        rendered.BodyHtml.ShouldContain("[Juan]");
        rendered.BodyHtml.ShouldContain("[Roadshow Madrid 2026]");
    }

    [Fact]
    public void Render_ReturnsTheSubjectSubstitutedLikeTheBody_WhenBothNameAPlaceholder()
    {
        var rendered = _engine.Render(
            "Invitación a {{evento}}",
            "<p>Le invitamos a {{evento}}.</p>",
            Values(("evento", "Roadshow Madrid 2026")));

        rendered.Subject.ShouldBe("Invitación a Roadshow Madrid 2026");
        rendered.BodyHtml.ShouldBe("<p>Le invitamos a Roadshow Madrid 2026.</p>");
    }

    [Fact]
    public void Render_ReturnsTheMarkerUntouched_WhenThePlaceholderIsUnknown()
    {
        var rendered = _engine.Render("{{desconocido}}", "<p>{{tampoco_existe}}</p>", Values());

        rendered.Subject.ShouldBe("{{desconocido}}");
        rendered.BodyHtml.ShouldBe("<p>{{tampoco_existe}}</p>");
    }

    [Fact]
    public void Render_ReturnsTheValue_WhenTheBracesHoldWhitespace()
    {
        // El escáner recorta el nombre, igual que el de notification-service, que es quien
        // sustituye en el camino de envío. Los dos dialectos tienen que resolver lo mismo, así
        // que esta tolerancia se fija acá en vez de dejarla al azar.
        var rendered = _engine.Render("{{ evento }}", "<p>{{  evento  }}</p>", Values(("evento", "Roadshow")));

        rendered.Subject.ShouldBe("Roadshow");
        rendered.BodyHtml.ShouldBe("<p>Roadshow</p>");
    }

    [Fact]
    public void Render_ReturnsTheMarkerUntouched_WhenTheMarkerIsEmpty()
    {
        _engine.Render(string.Empty, "<p>{{}} {{   }}</p>", Values()).BodyHtml
            .ShouldBe("<p>{{}} {{   }}</p>");
    }

    [Fact]
    public void Render_ReturnsAnEscapedValue_WhenTheMarkerHasTripleBraces()
    {
        // {{{x}}} casa solo por dentro: las llaves sobrantes quedan como texto y el valor sale
        // escapado igual. No existe la variante cruda.
        var rendered = _engine.Render(string.Empty, "{{{nombre}}}", Values(("nombre", "<b>")));

        rendered.BodyHtml.ShouldBe("{&lt;b&gt;}");
    }

    #endregion

    #region Escape

    [Fact]
    public void Render_ReturnsAnHtmlEscapedBody_WhenTheValueHoldsMarkup()
    {
        // El nombre llega de una hoja de cálculo que sube una persona: es un camino vivo desde
        // una celda hasta el cuerpo del correo.
        var rendered = _engine.Render(string.Empty, "<p>{{nombre}}</p>", Values(("nombre", "Juan <script>alert(1)</script>")));

        rendered.BodyHtml.ShouldNotContain("<script>");
        rendered.BodyHtml.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public void Render_ReturnsAnEscapedAmpersandInTheBody_WhenTheValueHoldsOne()
    {
        var rendered = _engine.Render(string.Empty, "<p>{{nombre}}</p>", Values(("nombre", "Sol & Playa")));

        rendered.BodyHtml.ShouldBe("<p>Sol &amp; Playa</p>");
    }

    [Fact]
    public void Render_ReturnsAccentedTextAsWritten_WhenTheValueHasAccents()
    {
        // El cuerpo viaja en UTF-8. Convertir cada tilde en una referencia numérica llenaría de
        // ruido una copia que es toda en español.
        var rendered = _engine.Render(string.Empty, "<p>{{lugar}}</p>", Values(("lugar", "España")));

        rendered.BodyHtml.ShouldBe("<p>España</p>");
    }

    [Fact]
    public void Render_ReturnsAnUnescapedSubject_WhenTheValueHoldsAnAmpersand()
    {
        // Es una cabecera y no lo lee ningún parser de HTML: escaparlo mostraría
        // "Feria &amp; Turismo" en la bandeja de entrada.
        var rendered = _engine.Render("{{evento}}", string.Empty, Values(("evento", "Feria & Turismo")));

        rendered.Subject.ShouldBe("Feria & Turismo");
    }

    [Fact]
    public void Render_ReturnsASubjectWithNoLineBreaks_WhenTheValueHoldsOne()
    {
        // Un salto de línea en un asunto puede terminar una cabecera y empezar otra.
        var rendered = _engine.Render("{{evento}}", string.Empty, Values(("evento", "Roadshow\r\nBcc: alguien@example.com")));

        rendered.Subject.ShouldNotContain("\n");
        rendered.Subject.ShouldNotContain("\r");
    }

    #endregion

    #region La caída neutra de {{nombre}}

    [Fact]
    public void Render_ReturnsTheNeutralGreeting_WhenTheNameIsMissing()
    {
        // Bloquear una campaña de mil porque una fila importada solo trae un correo es la cola
        // moviendo al perro. Saltarse a esa persona se descartó: la coordinadora la cree invitada.
        var rendered = _engine.Render(string.Empty, "<p>Estimado/a {{nombre}}:</p>", Values());

        rendered.BodyHtml.ShouldBe($"<p>Estimado/a {SupportedPlaceholder.NeutralRecipientName}:</p>");
    }

    [Fact]
    public void Render_ReturnsTheNeutralGreeting_WhenTheNameIsBlank()
    {
        var rendered = _engine.Render(string.Empty, "<p>{{nombre}}</p>", Values(("nombre", "   ")));

        rendered.BodyHtml.ShouldBe($"<p>{SupportedPlaceholder.NeutralRecipientName}</p>");
    }

    [Fact]
    public void Render_ReturnsTheName_WhenTheNameIsPresent()
    {
        var rendered = _engine.Render(string.Empty, "<p>{{nombre}}</p>", Values(("nombre", "Juan")));

        rendered.BodyHtml.ShouldBe("<p>Juan</p>");
    }

    [Fact]
    public void Render_ReturnsWithoutAddingTheNameToTheCallerValues_WhenTheNameIsMissing()
    {
        var values = Values();

        _engine.Render(string.Empty, "<p>{{nombre}}</p>", values);

        values.ShouldNotContainKey("nombre");
    }

    #endregion

    #region Guardas

    [Fact]
    public void Render_ThrowsArgumentNullException_WhenTheValuesAreNull()
    {
        Should.Throw<ArgumentNullException>(() => _engine.Render("s", "b", null!));
    }

    #endregion
}
