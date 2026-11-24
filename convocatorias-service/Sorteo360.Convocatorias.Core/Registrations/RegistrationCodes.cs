using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// Los dos códigos que lleva una inscripción, y que hacen dos trabajos distintos
/// (spec <c>06</c> §2.3).
/// <para>
/// <b>Juntarlos en uno no se puede.</b> Un código opaco se vuelve una credencial en cuanto entra
/// en una URL pública, y 43 caracteres de base64url no es algo que nadie lea de una escarapela.
/// Uno solo da o una escarapela ilegible o una credencial adivinable.
/// </para>
/// </summary>
public static class RegistrationCodes
{
    /// <summary>Cuántos bytes de azar lleva el QR. El mismo tamaño que el token del portal.</summary>
    private const int QrByteLength = 32;

    /// <summary>Con cuántos dígitos se escribe la secuencia. Crece sola si un año los pasa.</summary>
    private const int SequenceDigits = 5;

    /// <summary>Lo que va delante. Es el código que una persona dicta por teléfono.</summary>
    private const string Prefix = "REF";

    /// <summary>
    /// La secuencia de PostgreSQL de la que sale el número. <b>Una sola y no una por año</b>:
    /// una secuencia nunca entrega el mismo número dos veces por concurrida que esté y nunca
    /// bloquea a nadie, mientras que reiniciarla cada enero pediría o DDL en caliente o una fila
    /// contador que pusiera en fila india a todas las inscripciones del portal. El año va delante
    /// como etiqueta, y la pareja sigue siendo única cruce o no cruce el año.
    /// </summary>
    public const string SequenceName = "registration_reference";

    /// <summary>
    /// <c>MITUR-2026-00427</c>: corto, humano, impreso en la escarapela y dicho por teléfono.
    /// <b>No autoriza nada</b>, y por eso puede ser adivinable sin que eso le sirva a nadie.
    /// </summary>
    /// <param name="year">El año que se escribe delante.</param>
    /// <param name="sequence">El número que entregó <see cref="SequenceName"/>.</param>
    public static string Reference(int year, long sequence)
    {
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "La secuencia empieza en 1.");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}-{year:D4}-{sequence.ToString($"D{SequenceDigits}", CultureInfo.InvariantCulture)}");
    }

    /// <summary>
    /// 32 bytes de <see cref="RandomNumberGenerator"/> en base64url. Es lo que codifica el QR y
    /// la clave por la que responde su URL, así que <b>nunca se enseña como texto</b>.
    /// <para>
    /// <b>Se guarda en claro</b>, a diferencia del token del portal, y no es un descuido: un
    /// <c>&lt;img src&gt;</c> dentro de un correo tiene que ser una URL que se pueda pedir con
    /// un GET, así que el código aparece en los registros de acceso de todos modos y guardarlo
    /// hasheado solo impediría servir la entrada. Es un secreto de menor valor que el token, y
    /// la spec lo dice: presentar un QR no puede ser la única prueba de identidad en la puerta.
    /// </para>
    /// </summary>
    public static string MintQrCode() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(QrByteLength));
}
