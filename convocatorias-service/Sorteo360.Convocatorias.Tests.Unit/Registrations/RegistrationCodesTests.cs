using Sorteo360.Convocatorias.Core.Registrations;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Registrations;

/// <summary>
/// Los dos códigos de una inscripción (spec <c>06</c> §2.3), que hacen trabajos distintos:
/// uno se imprime y se dicta, el otro es la clave de una URL pública.
/// </summary>
public sealed class RegistrationCodesTests
{
    #region Reference

    /// <summary>
    /// La forma exacta de §2.3, porque se imprime en la escarapela y el portal la enseña tal
    /// cual: cambiarla rompe toda escarapela ya impresa.
    /// </summary>
    [Fact]
    public void Reference_ReturnsThePrintedShape_WhenGivenAYearAndASequence()
    {
        RegistrationCodes.Reference(2026, 427).ShouldBe("REF-2026-00427");
    }

    /// <summary>Cinco dígitos con ceros delante: todas las escarapelas de un año miden igual.</summary>
    [Theory]
    [InlineData(1, "REF-2026-00001")]
    [InlineData(99999, "REF-2026-99999")]
    public void Reference_ReturnsAPaddedSequence_WhenItIsBelowTheWidth(long sequence, string expected)
    {
        RegistrationCodes.Reference(2026, sequence).ShouldBe(expected);
    }

    /// <summary>
    /// Pasado el ancho crece en vez de truncar. Un código recortado sería otro código, y con
    /// <c>reference</c> único eso es una inscripción que no se puede escribir.
    /// </summary>
    [Fact]
    public void Reference_ReturnsMoreDigits_WhenTheSequenceOutgrowsTheWidth()
    {
        RegistrationCodes.Reference(2026, 1_000_000).ShouldBe("REF-2026-1000000");
    }

    /// <summary>
    /// El año va delante y la secuencia no reinicia: es una sola para todo el servicio, así que
    /// la primera inscripción de enero sigue el número de la última de diciembre.
    /// </summary>
    [Fact]
    public void Reference_ReturnsTheGivenYear_WhenTheSequenceCrossesIntoANewOne()
    {
        RegistrationCodes.Reference(2027, 428).ShouldBe("REF-2027-00428");
    }

    /// <summary>
    /// Una secuencia de PostgreSQL empieza en 1. Un cero o un negativo solo puede venir de que
    /// alguien la leyó de otro sitio, y dejarlo pasar imprimiría <c>MITUR-2026-00000</c>.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reference_ThrowsArgumentOutOfRangeException_WhenTheSequenceIsNotPositive(long sequence)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RegistrationCodes.Reference(2026, sequence));
    }

    #endregion

    #region QrCode

    /// <summary>32 bytes en base64url son 43 caracteres sin relleno.</summary>
    [Fact]
    public void MintQrCode_ReturnsFortyThreeCharacters_WhenItMints()
    {
        RegistrationCodes.MintQrCode().Length.ShouldBe(43);
    }

    /// <summary>
    /// El alfabeto de URL y sin relleno: el código entra en una ruta —la de la entrada y la de
    /// su imagen— y cualquier otro carácter habría que escaparlo en cada sitio que lo escriba.
    /// </summary>
    [Fact]
    public void MintQrCode_ReturnsOnlyUrlSafeCharacters_WhenItMints()
    {
        var code = RegistrationCodes.MintQrCode();

        code.ShouldNotContain("=");
        code.ShouldAllBe(character =>
            char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_');
    }

    /// <summary>
    /// Es la clave de una URL pública, así que tiene que ser azar y no un contador. Cien tiradas
    /// no prueban la entropía; lo que cobran es que nadie lo cambie por algo derivado del id.
    /// </summary>
    [Fact]
    public void MintQrCode_ReturnsADifferentCode_WhenItIsCalledAgain()
    {
        var codes = Enumerable.Range(0, 100).Select(_ => RegistrationCodes.MintQrCode()).ToArray();

        codes.Distinct(StringComparer.Ordinal).Count().ShouldBe(codes.Length);
    }

    #endregion
}
