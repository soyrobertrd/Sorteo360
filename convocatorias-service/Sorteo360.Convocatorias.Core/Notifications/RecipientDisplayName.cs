namespace Sorteo360.Convocatorias.Core.Notifications;

/// <summary>
/// El nombre al que va dirigido un correo, armado con el nombre y el apellido del contacto.
/// notification-service lo pone en la cabecera <c>To</c> (<c>Ana Pérez &lt;ana@…&gt;</c>) y lo
/// guarda en su historial. Es aparte del marcador <c>{{nombre}}</c> del cuerpo, que sigue
/// llevando solo el nombre de pila.
/// </summary>
public static class RecipientDisplayName
{
    /// <summary>El tope que cobra notification-service por persona.</summary>
    public const int MaxLength = 200;

    /// <summary>
    /// Nombre y apellido recortados y unidos por un espacio; la parte que falte se omite, y sin
    /// ninguna de las dos es null. Una fila importada puede traer solo la dirección.
    /// <para>
    /// Nombre y apellido admiten hasta 200 caracteres cada uno, así que la unión se corta a
    /// <see cref="MaxLength"/>: un nombre largo no puede tumbar el lote entero con un 400.
    /// </para>
    /// </summary>
    public static string? From(string? firstName, string? lastName)
    {
        var joined = string.Join(
            ' ',
            new[] { firstName?.Trim(), lastName?.Trim() }.Where(part => !string.IsNullOrEmpty(part)));

        if (joined.Length == 0)
            return null;

        return joined.Length <= MaxLength ? joined : joined[..MaxLength].TrimEnd();
    }
}
