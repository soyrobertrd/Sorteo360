using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Sorteo360.Convocatorias.Core.Portal;

/// <summary>
/// La única credencial del portal: 32 bytes de <see cref="RandomNumberGenerator"/> en base64url,
/// 43 caracteres (spec <c>06</c> §2.1).
/// <para>
/// <b>El texto plano no se guarda en ningún sitio.</b> Se arma el enlace con él, se guarda solo su
/// SHA-256 y no se vuelve a necesitar, así que un volcado de la base no entrega ni un enlace que
/// funcione. Es la regla entera que la spec <c>06</c> existe para proteger.
/// </para>
/// <para>
/// El de registro y el de baja son dos tokens distintos a propósito: el de registro está pensado
/// para reenviarse, así que compartirlo haría que el clic de quien recibe el reenvío diera de baja
/// a la persona original (spec <c>06</c> §2.4).
/// </para>
/// </summary>
public static class PortalToken
{
    /// <summary>Cuántos bytes de azar. No es ajustable: es el tamaño que fija la spec.</summary>
    private const int ByteLength = 32;

    /// <summary>
    /// Uno nuevo, con su hash ya calculado. El alfabeto de URL y sin relleno lo pone
    /// <see cref="Base64Url"/> del framework: cabe en un enlace sin escaparse.
    /// </summary>
    public static MintedToken Mint()
    {
        var plaintext = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength));

        return new MintedToken(plaintext, HashOf(plaintext));
    }

    /// <summary>
    /// El SHA-256 del texto plano, que es lo único que se guarda y por lo que se busca. Sobre los
    /// caracteres del token y no sobre los bytes de los que salió: es lo que llega en la petición
    /// del portal, y hacer el rodeo inverso sería una decodificación más donde puede fallar.
    /// <para>
    /// Es público porque el portal tiene que calcular el mismo hash para buscar por él: quien
    /// resuelve un token recibe los caracteres y no los bytes, y una segunda copia de esta línea
    /// del otro lado es como el día que alguien cambie el algoritmo mata todo enlace vivo.
    /// </para>
    /// </summary>
    public static byte[] HashOf(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);

        return SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
    }
}

/// <summary>
/// Un token recién acuñado: el texto plano, que va al enlace y se olvida, y el hash, que es lo
/// único que llega a la base.
/// </summary>
public sealed record MintedToken(string Plaintext, byte[] Hash);
