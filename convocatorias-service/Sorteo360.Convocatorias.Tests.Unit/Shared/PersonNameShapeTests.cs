using Sorteo360.Convocatorias.Core.Shared;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Shared;

/// <summary>
/// Cuándo dos nombres son el de la misma persona, que es lo que decide si una inscripción marca
/// <c>contact_details_differ</c> (spec <c>06</c> §4.2).
/// <para>
/// La regla entera es <b>mayúsculas y tildes aparte, sobre el nombre completo recortado</b>. Cada
/// regla de más —juntar espacios de dentro, aceptar el apellido delante, quitar los títulos— es
/// una diferencia real que dejaría de llegarle a la persona que revisa la lista a mano.
/// </para>
/// </summary>
public sealed class PersonNameShapeTests
{
    /// <summary>
    /// Lo que escribe un desconocido en el portal contra lo que ya está en el CRM. Ninguno de
    /// estos pares merece el trabajo de nadie.
    /// </summary>
    [Theory]
    // Tal cual.
    [InlineData("Juan Pérez", "Juan Pérez")]
    // Mayúsculas: el formulario de un móvil las pone solas.
    [InlineData("JUAN PÉREZ", "juan pérez")]
    // Tildes: casi nadie las escribe en un teclado de teléfono.
    [InlineData("Juan Perez", "Juan Pérez")]
    // La eñe entra por la misma puerta, que es lo que evita avisar por "Peña" contra "Pena".
    [InlineData("Luis Pena", "Luis Peña")]
    // Espacios de los bordes, que es lo que deja pegado un copiar y pegar.
    [InlineData("  Juan Pérez  ", "Juan Pérez")]
    public void Matches_ReturnsTrue_WhenOnlyCaseOrAccentsDiffer(string typed, string onFile) =>
        PersonNameShape.Matches(typed, onFile).ShouldBeTrue();

    /// <summary>
    /// Las diferencias que sí son diferencias. Todas acaban en la lista que <c>07</c> saca para
    /// revisar a mano, y ninguna se resuelve sola: no sabemos si es una corrección o una errata,
    /// y por eso el CRM no se toca.
    /// </summary>
    [Theory]
    // Una inicial en vez del nombre: el caso de la spec, "J. Perez" contra "Juan Pérez".
    [InlineData("J. Perez", "Juan Pérez")]
    // Un apellido distinto.
    [InlineData("Juan Gómez", "Juan Pérez")]
    // Un nombre de más, que puede ser el segundo apellido o puede ser otra persona.
    [InlineData("Juan Pérez de la Cruz", "Juan Pérez")]
    // Dado la vuelta: el buzón de una empresa suele estar a nombre de quien lo abrió.
    [InlineData("Pérez Juan", "Juan Pérez")]
    // Vacío contra algo: un CRM sin nombre no coincide con quien sí lo escribió.
    [InlineData("", "Juan Pérez")]
    [InlineData("Juan Pérez", "   ")]
    public void Matches_ReturnsFalse_WhenTheNamesAreActuallyDifferent(string typed, string onFile) =>
        PersonNameShape.Matches(typed, onFile).ShouldBeFalse();

    /// <summary>
    /// Dos vacíos coinciden, y eso es a propósito: si ninguno de los dos lados dice nada, no hay
    /// discrepancia que enseñarle a nadie.
    /// </summary>
    [Fact]
    public void Matches_ReturnsTrue_WhenNeitherSideSaysAnything() =>
        PersonNameShape.Matches("  ", "").ShouldBeTrue();

    /// <summary>
    /// La sobrecarga de cuatro campos, que es la que usan los dos sitios que comparan de verdad:
    /// el nombre completo es <b>cada mitad recortada</b> y unidas por un espacio.
    /// <para>
    /// Recortar solo el resultado no alcanza, y es lo que arregla esta sobrecarga: un espacio de
    /// sobra al final del nombre de pila sobrevive dentro del texto unido, así que
    /// <c>"Juan "</c> más <c>"Pérez"</c> daría <c>"Juan  Pérez"</c> y discreparía de la ficha que
    /// dice exactamente lo mismo. La fila se guarda recortada, así que ese aviso señalaría a una
    /// persona cuyos datos ya coinciden.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Juan ", "Pérez", "Juan", "Pérez")]
    [InlineData(" Juan", " Pérez ", "JUAN", "perez")]
    public void Matches_ReturnsTrue_WhenEachHalfOfTheNameOnlyDiffersByPadding(
        string firstLeft,
        string lastLeft,
        string firstRight,
        string lastRight) =>
        PersonNameShape.Matches(firstLeft, lastLeft, firstRight, lastRight).ShouldBeTrue();

    /// <summary>
    /// Y la sobrecarga <b>sí junta las dos mitades</b> antes de comparar, porque §4.2 compara el
    /// nombre completo y no campo a campo. Quien mete su segundo apellido en la casilla del nombre
    /// se llama igual, y "Lic. Juan Pérez de la Cruz" no tiene punto de corte fiable (§4.1): avisar
    /// por dónde cayó la raya sería ruido en una lista que alguien tiene que mirar entera.
    /// </summary>
    [Fact]
    public void Matches_ReturnsTrue_WhenTheHalvesAreSplitDifferentlyOverTheSameFullName() =>
        PersonNameShape.Matches("Juan Pérez", "de la Cruz", "Juan", "Pérez de la Cruz").ShouldBeTrue();
}
