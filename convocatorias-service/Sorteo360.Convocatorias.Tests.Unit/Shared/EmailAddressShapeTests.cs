using Sorteo360.Convocatorias.Core.Shared;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Shared;

/// <summary>
/// La forma de una dirección, la regla que comparten la carga de listas y el envío. Es laxa a
/// propósito: lo que caza es la columna equivocada y la dirección imposible, no la errata.
/// </summary>
public sealed class EmailAddressShapeTests
{
    [Theory]
    [InlineData("juan@expedia.com")]
    [InlineData("juan.perez+feria@sub.dominio.gob.do")]
    [InlineData("J@x.io")]
    public void IsWellFormed_ReturnsTrue_WhenTheValueLooksLikeAnAddress(string value) =>
        EmailAddressShape.IsWellFormed(value).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // La columna equivocada: un teléfono, un nombre, una empresa.
    [InlineData("809-555-0100")]
    [InlineData("Juan Pérez")]
    // Y las direcciones que no pueden existir.
    [InlineData("sin-arroba.com")]
    [InlineData("@expedia.com")]
    [InlineData("juan@")]
    [InlineData("juan@expedia")]
    [InlineData("juan@.com")]
    [InlineData("juan@expedia.")]
    [InlineData("no-such@@bad.com")]
    [InlineData("juan perez@expedia.com")]
    [InlineData("juan@expedia.com\nBcc: otro@x.com")]
    public void IsWellFormed_ReturnsFalse_WhenTheValueCannotBeAnAddress(string? value) =>
        EmailAddressShape.IsWellFormed(value).ShouldBeFalse();

    /// <summary>
    /// La clave por la que dos direcciones cuentan como la misma: recorte y minúsculas, y nada
    /// más. Ni puntos de gmail ni <c>+etiqueta</c>, porque eso decide quién es una sola persona
    /// y esa decisión no vive acá. La usan el deduplicado de una carga y la unicidad de una
    /// inscripción, que es una restricción de la base (spec <c>06</c> §4.1).
    /// </summary>
    [Theory]
    [InlineData("  Ana@Acme.com ", "ana@acme.com")]
    [InlineData("a.n.a+lista@gmail.com", "a.n.a+lista@gmail.com")]
    public void Normalize_ReturnsTheTrimmedLowercasedAddress_WhenGivenAnyCasingOrPadding(
        string input,
        string expected) =>
        EmailAddressShape.Normalize(input).ShouldBe(expected);
}
