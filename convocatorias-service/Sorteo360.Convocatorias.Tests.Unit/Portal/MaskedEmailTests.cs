using Sorteo360.Convocatorias.Core.Portal;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Portal;

/// <summary>
/// Lo que ve la página de baja: la dirección sobre la que actúa, enmascarada, para que quien
/// recibió un reenvío vea una que no es la suya y se detenga (spec <c>06</c> §2.4).
/// </summary>
public sealed class MaskedEmailTests
{
    [Fact]
    public void Of_KeepsFirstLetterAndWholeDomain_WhenAddressIsOrdinary()
    {
        MaskedEmail.Of("ana@acme.com").ShouldBe("a***@acme.com");
    }

    [Fact]
    public void Of_KeepsOnlyTheFirstLetter_WhenLocalPartIsLong()
    {
        MaskedEmail.Of("juan.perez@correo.gob.do").ShouldBe("j***@correo.gob.do");
    }

    [Fact]
    public void Of_MasksTheSingleLetter_WhenLocalPartIsOneCharacter()
    {
        MaskedEmail.Of("a@acme.com").ShouldBe("a***@acme.com");
    }
}
