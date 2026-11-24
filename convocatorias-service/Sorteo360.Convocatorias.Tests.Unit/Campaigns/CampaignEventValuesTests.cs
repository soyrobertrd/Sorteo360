using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Rendering;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// La copia de los tres valores del evento que el lanzamiento guarda en la fila, y que el worker
/// vuelve a leer para armar la bolsa compartida del lote.
/// </summary>
public sealed class CampaignEventValuesTests
{
    [Fact]
    public void ToSharedData_ReturnsWhatFromWasGiven_WhenRoundTripped()
    {
        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SupportedPlaceholder.EventName] = "Roadshow Madrid 2026",
            [SupportedPlaceholder.EventLocation] = "Madrid, España",
            [SupportedPlaceholder.EventDates] = "12 de junio de 2026",
        };

        CampaignEventValues.From(placeholders).ToSharedData().ShouldBe(placeholders);
    }

    /// <summary>
    /// Una clave ausente sale vacía y no explota. Para quien comprueba, ausente y vacío significan
    /// lo mismo, y el lanzamiento ya rechazó antes de llegar acá si faltaba alguno.
    /// </summary>
    [Fact]
    public void From_ReturnsEmptyValues_WhenTheKeysAreMissing()
    {
        var values = CampaignEventValues.From(new Dictionary<string, string>(StringComparer.Ordinal));

        values.Name.ShouldBeEmpty();
        values.Location.ShouldBeEmpty();
        values.Dates.ShouldBeEmpty();
    }

    /// <summary>
    /// La bolsa compartida trae siempre las tres claves, que es lo que deja al transporte
    /// sustituir un marcador sin preguntarse si la clave llegó.
    /// </summary>
    [Fact]
    public void ToSharedData_ReturnsTheThreeEventKeys_WhenCalled()
    {
        new CampaignEventValues("Feria", "Santo Domingo", "hoy").ToSharedData().Keys
            .ShouldBe(SupportedPlaceholder.EventLevel, ignoreOrder: true);
    }
}
