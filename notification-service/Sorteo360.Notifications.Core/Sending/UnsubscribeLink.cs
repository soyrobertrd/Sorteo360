namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// De dónde sale el <c>List-Unsubscribe</c> de un mensaje.
/// <para>
/// El servicio no puede distinguir una persona de dentro del MITUR de una de fuera, y no debe: las
/// dos llegan como <c>{ email, data }</c>. Lo que sí puede mirar es si quien llama le mandó un
/// enlace de baja para esa persona, que es exactamente la misma frontera vista desde el único lado
/// que el servicio alcanza: quien está fuera lo trae porque su correo lo necesita, y quien está
/// dentro no lo trae porque no puede darse de baja del correo de su trabajo.
/// </para>
/// </summary>
public static class UnsubscribeLink
{
    /// <summary>
    /// La clave con la que viaja el enlace. Es la misma que el cuerpo nombra como
    /// <c>{{enlace_baja}}</c>, así que la cabecera y el enlace visible no se pueden separar.
    /// </summary>
    public const string DataKey = "enlace_baja";

    /// <summary>
    /// El enlace de esta persona, o null si no trae. Un valor en blanco cuenta como no traerlo:
    /// una cabecera vacía es peor que ninguna.
    /// </summary>
    public static string? From(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values.TryGetValue(DataKey, out var url) && !string.IsNullOrWhiteSpace(url)
            ? url.Trim()
            : null;
    }
}
