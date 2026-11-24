using Sorteo360.Notifications.Core.Sending;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Sending;

/// <summary>
/// De dónde sale el <c>List-Unsubscribe</c>. El servicio no distingue a quien está dentro del MITUR
/// de quien está fuera, y no debe: lo único que mira es si quien llama mandó un enlace de baja para
/// esa persona.
/// </summary>
public sealed class UnsubscribeLinkTests
{
    [Fact]
    public void From_ReturnsTheLink_WhenTheRecipientIsExternal()
    {
        var url = UnsubscribeLink.From(new Dictionary<string, string>
        {
            ["nombre"] = "Juan",
            ["enlace_baja"] = "https://app.sorteo360.local/baja?t=abc"
        });

        url.ShouldBe("https://app.sorteo360.local/baja?t=abc");
    }

    [Fact]
    public void From_ReturnsNothing_WhenTheRecipientIsInternal()
    {
        // No se puede dar de baja del correo de su trabajo, así que quien llama no manda enlace.
        var url = UnsubscribeLink.From(new Dictionary<string, string> { ["nombre"] = "María" });

        url.ShouldBeNull();
    }

    [Fact]
    public void From_ReturnsNothing_WhenTheLinkIsBlank() =>
        // Una cabecera vacía es peor que ninguna cabecera.
        UnsubscribeLink.From(new Dictionary<string, string> { ["enlace_baja"] = "   " }).ShouldBeNull();

    [Fact]
    public void DataKey_ReturnsTheKeyTheBodyNames_WhenRead() =>
        // La cabecera y el {{enlace_baja}} visible no se pueden separar.
        UnsubscribeLink.DataKey.ShouldBe("enlace_baja");
}
