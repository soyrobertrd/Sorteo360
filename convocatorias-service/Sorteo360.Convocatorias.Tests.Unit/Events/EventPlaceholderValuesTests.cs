using System.Globalization;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Rendering;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Events;

/// <summary>
/// De dónde sale el valor de cada marcador de grano de evento (spec <c>02</c> §2, spec <c>01</c>
/// §4.1). Lo que se prueba acá no es el formato por el formato: es que un evento al que le falta
/// el dato produzca un valor <b>vacío</b> y no un texto plausible, porque el vacío es lo único
/// que el lanzamiento sabe rechazar.
/// </summary>
public sealed class EventPlaceholderValuesTests
{
    private static readonly Guid EventId = Guid.Parse("4e9a5b6c-8d3f-4012-9c2d-3e4f50617283");

    #region {{evento}}

    [Fact]
    public void From_ReturnsTheEventNameForEvento_WhenTheNameIsSet()
    {
        Values(WithName("Roadshow Madrid 2026"))[SupportedPlaceholder.EventName]
            .ShouldBe("Roadshow Madrid 2026");
    }

    [Fact]
    public void From_ReturnsAnEmptyEvento_WhenTheNameIsBlank()
    {
        // Un nombre en blanco no es un evento sin nombre que se pueda rellenar: es el dato que
        // falta, y el lanzamiento tiene que poder verlo como tal.
        Values(WithName("   "))[SupportedPlaceholder.EventName].ShouldBeEmpty();
    }

    #endregion

    #region {{lugar}}

    [Fact]
    public void From_ReturnsTheCityAndCountryForLugar_WhenThereIsOneCity()
    {
        Values(WithCities(new EventCity("Madrid", "España", null, 1)))[SupportedPlaceholder.EventLocation]
            .ShouldBe("Madrid, España");
    }

    /// <summary>
    /// Una campaña por evento significa un correo que cubre todas las ciudades, así que
    /// <c>{{lugar}}</c> es una lista (spec <c>01</c> §4.1).
    /// </summary>
    [Fact]
    public void From_ReturnsTheCitiesJoinedInDisplayOrder_WhenThereAreSeveral()
    {
        var summary = WithCities(
            new EventCity("Barcelona", "España", null, 2),
            new EventCity("Madrid", "España", null, 1));

        Values(summary)[SupportedPlaceholder.EventLocation]
            .ShouldBe("Madrid, España; Barcelona, España");
    }

    [Fact]
    public void From_ReturnsTheUnorderedCityLast_WhenACityHasNoDisplayOrder()
    {
        var summary = WithCities(
            new EventCity("Sevilla", "España", null, null),
            new EventCity("Madrid", "España", null, 1));

        Values(summary)[SupportedPlaceholder.EventLocation]
            .ShouldBe("Madrid, España; Sevilla, España");
    }

    [Fact]
    public void From_ReturnsAnEmptyLugar_WhenTheEventHasNoCities()
    {
        // El caso que de verdad ocurre: el save-the-date sale antes de que se fije el recorrido.
        Values(WithCities())[SupportedPlaceholder.EventLocation].ShouldBeEmpty();
    }

    #endregion

    #region {{fecha}}

    [Fact]
    public void From_ReturnsThatDayForFecha_WhenTheEventLastsOneDay()
    {
        Values(WithDates(new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 12)))[SupportedPlaceholder.EventDates]
            .ShouldBe("12 de junio de 2026");
    }

    [Fact]
    public void From_ReturnsBothEndsForFecha_WhenTheDatesSpanARange()
    {
        Values(WithDates(new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 14)))[SupportedPlaceholder.EventDates]
            .ShouldBe("del 12 de junio de 2026 al 14 de junio de 2026");
    }

    /// <summary>
    /// El mes va en español pase lo que pase en la máquina que corre el proceso. Sin cultura
    /// fija, un contenedor en inglés mandaría "12 de June de 2026" a mil personas.
    /// </summary>
    [Fact]
    public void From_ReturnsASpanishMonth_WhenTheAmbientCultureIsEnglish()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        try
        {
            Values(WithDates(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5)))[SupportedPlaceholder.EventDates]
                .ShouldBe("5 de enero de 2026");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    /// <summary>
    /// Las dos fechas no son anulables, así que un evento sin ellas llega con el día cero. Eso no
    /// es una fecha: es el dato que falta, y tiene que salir vacío para que el lanzamiento lo
    /// pueda rechazar en vez de mandar "se celebrará el 1 de enero de 0001".
    /// </summary>
    [Fact]
    public void From_ReturnsAnEmptyFecha_WhenTheDateIsUnset()
    {
        Values(WithDates(default, default))[SupportedPlaceholder.EventDates].ShouldBeEmpty();
    }

    #endregion

    #region El conjunto

    [Fact]
    public void From_ReturnsExactlyTheEventLevelPlaceholders_WhenCalled()
    {
        Values(WithName("Roadshow Madrid 2026")).Keys
            .ShouldBe(SupportedPlaceholder.EventLevel, ignoreOrder: true);
    }

    #endregion

    /// <summary>Los tres valores de grano de evento de ese evento.</summary>
    private static IReadOnlyDictionary<string, string> Values(EventSummary summary) =>
        EventPlaceholderValues.From(summary);

    /// <summary>El evento de referencia, con otro nombre.</summary>
    private static EventSummary WithName(string name) => Summary() with { Name = name };

    /// <summary>El evento de referencia, con otro recorrido. Sin argumentos, sin ninguna ciudad.</summary>
    private static EventSummary WithCities(params EventCity[] cities) => Summary() with { Cities = cities };

    /// <summary>El evento de referencia, con otras fechas.</summary>
    private static EventSummary WithDates(DateOnly start, DateOnly end) =>
        Summary() with { StartDate = start, EndDate = end };

    /// <summary>Un evento completo: nombre, una ciudad y un tramo de fechas.</summary>
    private static EventSummary Summary() => new()
    {
        Id = EventId,
        Name = "Roadshow Madrid 2026",
        Type = "Roadshow",
        Status = EventStatus.InProgress,
        Language = "es",
        Visibility = EventVisibility.Public,
        StartDate = new DateOnly(2026, 6, 12),
        EndDate = new DateOnly(2026, 6, 14),
        Cities = [new EventCity("Madrid", "España", null, 1)],
    };
}
