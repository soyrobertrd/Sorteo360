using Sorteo360.Convocatorias.Core.CheckIns;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.CheckIns;

/// <summary>
/// La caja de búsqueda de la lista de acceso (spec <c>09</c> §5.1, enmendada el 2026-10-10): cada
/// palabra en el nombre, el apellido o la referencia, sin mayúsculas ni tildes, o la dirección
/// entera y exacta.
/// </summary>
public sealed class DoorListSearchTests
{
    private static readonly DoorListRow Maria = new(
        Guid.NewGuid(), "María", "García", "Viajes del Caribe SRL", "REF-2026-00427",
        "maria.garcia@viajes.com", CheckedInAt: null);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Matches_ReturnsTrue_WhenTheSearchIsEmpty(string? search)
    {
        new DoorListSearch(search).Matches(Maria).ShouldBeTrue();
    }

    /// <summary>
    /// Depende de ICU: una imagen con globalización invariante volvería ordinal la comparación sin
    /// avisar, y esta prueba es la que avisa.
    /// </summary>
    [Theory]
    [InlineData("garcia")]
    [InlineData("GARCÍA")]
    [InlineData("marí")]
    [InlineData("00427")]
    public void Matches_ReturnsTrue_WhenOneWordIsInANameOrTheReferenceIgnoringCaseAndAccents(string search)
    {
        new DoorListSearch(search).Matches(Maria).ShouldBeTrue();
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenTheWordIsNunezAndTheNameIsNunez()
    {
        var row = Maria with { LastName = "Núñez" };

        new DoorListSearch("nunez").Matches(row).ShouldBeTrue();
    }

    [Theory]
    [InlineData("maría garcía")]
    [InlineData("garcía maría")]
    [InlineData("  garcía   00427 ")]
    public void Matches_ReturnsTrue_WhenEveryWordIsInSomeField(string search)
    {
        new DoorListSearch(search).Matches(Maria).ShouldBeTrue();
    }

    [Theory]
    [InlineData("maría pérez")]
    [InlineData("garcía 00999")]
    public void Matches_ReturnsFalse_WhenOneWordIsInNoField(string search)
    {
        new DoorListSearch(search).Matches(Maria).ShouldBeFalse();
    }

    /// <summary>La empresa se ve en la lista pero no se busca: la spec nombra tres campos.</summary>
    [Fact]
    public void Matches_ReturnsFalse_WhenTheWordIsOnlyInTheCompany()
    {
        new DoorListSearch("caribe").Matches(Maria).ShouldBeFalse();
    }

    [Theory]
    [InlineData("maria.garcia@viajes.com")]
    [InlineData("  Maria.Garcia@VIAJES.com ")]
    public void Matches_ReturnsTrue_WhenTheWholeInputIsTheNormalisedEmail(string search)
    {
        new DoorListSearch(search).Matches(Maria).ShouldBeTrue();
    }

    /// <summary>Exacta y no por fragmento: la caja no sirve para sacar direcciones letra a letra.</summary>
    [Theory]
    [InlineData("maria.garcia@")]
    [InlineData("@viajes.com")]
    [InlineData("viajes")]
    public void Matches_ReturnsFalse_WhenTheInputIsOnlyAFragmentOfTheEmail(string search)
    {
        new DoorListSearch(search).Matches(Maria).ShouldBeFalse();
    }
}
