using System.Buffers;
using MimeKit;

namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Si una dirección es una dirección. Lo decide el mismo parser que abrirá la transacción SMTP,
/// así que lo que acá se acepta es exactamente lo que allá se puede direccionar.
/// <para>
/// Es la única cosa que se puede rechazar por persona en el momento de aceptar. Nada de lo que
/// sigue —si el buzón existe, si acepta correo, si está suprimido— se puede saber sin enviar.
/// </para>
/// </summary>
public static class EmailAddressSyntax
{
    /// <summary>
    /// Lo que no cabe en este campo: un nombre visible, o más de una dirección. El nombre del
    /// remitente es otro campo, y una persona por fila es la regla de todo el servicio.
    /// </summary>
    private static readonly SearchValues<char> Forbidden =
        SearchValues.Create("<>,;\"");

    /// <summary>
    /// Opciones del parser con las direcciones sin dominio fuera. El parser las admite por omisión
    /// porque el correo interno de un servidor las usa, y acá no sirven: el mensaje sale a internet.
    /// </summary>
    private static readonly ParserOptions Options = WithoutLocalAddresses();

    /// <summary>
    /// Comprueba que el texto sea una sola dirección de buzón. Rechaza lo vacío, las listas y
    /// cualquier cosa que el parser no reconozca.
    /// </summary>
    public static bool IsValid(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        // El parser admite "Nombre <correo@dominio>", que acá no vale: la dirección es un campo
        // y el nombre es otro.
        if (address.AsSpan().IndexOfAny(Forbidden) >= 0)
            return false;

        return MailboxAddress.TryParse(Options, address.Trim(), out var mailbox)
               && mailbox.Address.Length > 0;
    }

    /// <summary>Clona las opciones por omisión y exige dominio.</summary>
    private static ParserOptions WithoutLocalAddresses()
    {
        var options = ParserOptions.Default.Clone();
        options.AllowAddressesWithoutDomain = false;

        return options;
    }
}
