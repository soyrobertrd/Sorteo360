using Sorteo360.Convocatorias.Core.Notifications;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Notifications;

/// <summary>
/// El nombre al que va dirigido un correo: nombre y apellido del contacto, como lo guarda el
/// historial de notification-service y como sale en la cabecera <c>To</c>.
/// </summary>
public sealed class RecipientDisplayNameTests
{
    [Fact]
    public void From_ReturnsFirstAndLastNameJoined_WhenBothArePresent()
    {
        RecipientDisplayName.From("  Ana ", " Pérez  ").ShouldBe("Ana Pérez");
    }

    [Theory]
    [InlineData("Ana", "", "Ana")]
    [InlineData("", "Pérez", "Pérez")]
    [InlineData(null, "  Pérez", "Pérez")]
    public void From_ReturnsTheOnePartPresent_WhenTheOtherIsBlank(string? firstName, string? lastName, string expected)
    {
        RecipientDisplayName.From(firstName, lastName).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "   ")]
    public void From_ReturnsNull_WhenTheContactHasNoName(string? firstName, string? lastName)
    {
        // Sin nombre no se manda nada: la pantalla muestra la dirección, nunca un guion.
        RecipientDisplayName.From(firstName, lastName).ShouldBeNull();
    }

    [Fact]
    public void From_ReturnsTheFirst200Characters_WhenTheJoinedNameIsLonger()
    {
        // Nombre y apellido admiten 200 cada uno y notification-service rechaza más de 200 en
        // total. Un nombre largo no puede tumbar un lote de mil personas.
        var name = RecipientDisplayName.From(new string('a', 150), new string('b', 150));

        name.ShouldBe(new string('a', 150) + " " + new string('b', 49));
    }
}
