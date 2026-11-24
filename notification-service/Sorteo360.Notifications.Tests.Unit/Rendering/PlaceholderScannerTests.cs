using Sorteo360.Notifications.Core.Rendering;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Rendering;

/// <summary>
/// El lado de lectura del motor: qué claves nombra un texto. Es lo que permite comprobar antes
/// del 202 que quien llama mandó un valor para cada marcador, sin que el servicio tenga una lista
/// de claves legales.
/// </summary>
public sealed class PlaceholderScannerTests
{
    [Fact]
    public void Scan_ReturnsTheKeysOfTheSubjectAndTheBody_WhenBothHaveMarkers()
    {
        var keys = PlaceholderScanner.Scan("Cotización de {{evento}}", "<p>Hola {{nombre}}</p>");

        keys.ShouldBe(["evento", "nombre"]);
    }

    [Fact]
    public void Scan_ReturnsEachKeyOnceInOrder_WhenAKeyRepeats()
    {
        var keys = PlaceholderScanner.Scan("{{b}} {{a}} {{b}}");

        keys.ShouldBe(["b", "a"]);
    }

    [Fact]
    public void Scan_ReturnsTheTrimmedKey_WhenTheBracesHoldSpaces()
    {
        var keys = PlaceholderScanner.Scan("{{  evento  }}");

        keys.ShouldBe(["evento"]);
    }

    [Fact]
    public void Scan_ReturnsBothKeys_WhenTheyDifferOnlyInCase()
    {
        var keys = PlaceholderScanner.Scan("{{evento}} {{Evento}}");

        keys.ShouldBe(["evento", "Evento"]);
    }

    [Theory]
    [InlineData("{{ sin cierre")]
    [InlineData("cierre sin apertura }}")]
    [InlineData("{{}}")]
    [InlineData("{{   }}")]
    [InlineData("")]
    public void Scan_ReturnsNothing_WhenThereIsNoMarker(string text)
    {
        var keys = PlaceholderScanner.Scan(text);

        keys.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_ReturnsNothing_WhenTheTextIsNull()
    {
        var keys = PlaceholderScanner.Scan("{{evento}}", null);

        keys.ShouldBe(["evento"]);
    }

    [Fact]
    public void Scan_ReturnsAPlainKey_WhenTheMarkerHasTripleBraces()
    {
        var keys = PlaceholderScanner.Scan("{{{evento}}}");

        keys.ShouldBe(["evento"]);
    }
}
