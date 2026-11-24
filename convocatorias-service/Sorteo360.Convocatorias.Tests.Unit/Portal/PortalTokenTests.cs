using System.Security.Cryptography;
using System.Text;
using Sorteo360.Convocatorias.Core.Portal;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Portal;

/// <summary>
/// La única credencial del portal (spec <c>06</c> §2.1). Lo que se cobra acá es su forma y que
/// de ella solo salga un hash: el texto plano se va dentro del enlace y no se guarda.
/// </summary>
public sealed class PortalTokenTests
{
    /// <summary>
    /// 32 bytes en base64url sin relleno son 43 caracteres, y ninguno de ellos necesita
    /// escaparse dentro de una URL.
    /// </summary>
    [Fact]
    public void Mint_ReturnsFortyThreeUrlSafeCharacters_WhenCalled()
    {
        var token = PortalToken.Mint();

        token.Plaintext.Length.ShouldBe(43);
        token.Plaintext.ShouldNotContain("=");
        token.Plaintext.ShouldNotContain("+");
        token.Plaintext.ShouldNotContain("/");
        token.Plaintext.ShouldAllBe(character =>
            char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_');
    }

    /// <summary>Uno por llamada. Es azar del sistema, no una secuencia que alguien pueda seguir.</summary>
    [Fact]
    public void Mint_ReturnsADifferentToken_WhenCalledAgain()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => PortalToken.Mint().Plaintext).ToList();

        tokens.Distinct(StringComparer.Ordinal).Count().ShouldBe(tokens.Count);
    }

    /// <summary>
    /// El hash es el SHA-256 del texto plano, que es lo que llega en la petición del portal: es
    /// por lo que se busca la fila, y lo único que queda si alguien se lleva la base.
    /// </summary>
    [Fact]
    public void Mint_ReturnsTheSha256OfThePlaintext_WhenCalled()
    {
        var token = PortalToken.Mint();

        token.Hash.ShouldBe(SHA256.HashData(Encoding.UTF8.GetBytes(token.Plaintext)));
        token.Hash.Length.ShouldBe(32);
    }
}
