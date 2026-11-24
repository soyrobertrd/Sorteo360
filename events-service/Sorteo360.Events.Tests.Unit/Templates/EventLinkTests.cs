using Sorteo360.Events.Core.Templates;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Templates;

public sealed class EventLinkTests
{
    private static readonly Guid EventId = new("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void For_ReturnsTheEventRoute_WhenTheBaseIsValid()
    {
        EventLink.For("https://app.sorteo360.local", EventId)
            .ShouldBe("https://app.sorteo360.local/eventos/11111111-2222-3333-4444-555555555555");
    }

    [Fact]
    public void For_ReturnsTheSameLink_WhenTheBaseHasATrailingSlash()
    {
        // Las dos formas son razonables en una variable de entorno, y una barra doble en un correo
        // se ve como un error de la plataforma aunque el navegador la tolere.
        EventLink.For("https://app.sorteo360.local/", EventId)
            .ShouldBe(EventLink.For("https://app.sorteo360.local", EventId));
    }

    [Fact]
    public void For_ReturnsALinkWithThePort_WhenTheBaseCarriesOne()
    {
        EventLink.For("http://localhost:5175", EventId)
            .ShouldBe("http://localhost:5175/eventos/11111111-2222-3333-4444-555555555555");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void For_Throws_WhenTheBaseIsMissing(string baseUrl)
    {
        // Falla al armar el enlace en lugar de emitir "/eventos/{id}", que saldría igual en el
        // correo y solo se descubriría con alguien haciendo clic. De todos modos la sección se
        // valida al arrancar, así que esto es el segundo cerrojo.
        Should.Throw<ArgumentException>(() => EventLink.For(baseUrl, EventId));
    }
}
