namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// El freno que impide mandar correo de verdad desde un entorno que no debe mandarlo. Se comprueba
/// al arrancar, antes de drenar una sola fila, y el servicio se niega a levantar si no pasa.
/// <para>
/// Mira la <b>credencial</b> y no el hostname. Un hostname es una suposición sobre a dónde apunta
/// un nombre: el DNS se mueve, un alias de Compose se renombra, y el del proveedor resuelve a doce
/// direcciones. La credencial no es una suposición: la API key <b>es</b> la contraseña SMTP, así
/// que sin ella no hay envío real posible por ninguna ruta. El freno rechaza la única
/// configuración en la que el accidente es físicamente posible.
/// </para>
/// <para>
/// Es una variable explícita y no <c>ASPNETCORE_ENVIRONMENT != Production</c> porque el día que IT
/// abra el 587 se quiere exactamente un envío real de prueba <b>desde stage</b>, y un freno por
/// nombre de entorno obligaría a comprar ese envío renombrando stage como Production: una mentira
/// que arrastra migraciones, exposición de la documentación y niveles de log.
/// </para>
/// </summary>
public static class SmtpRelayGuard
{
    /// <summary>Lo que se lee en el log del arranque fallido.</summary>
    public const string RefusalMessage =
        "Smtp__AllowExternalRelay está en false y Smtp__Password no está vacía: este entorno no " +
        "tiene permiso para mandar correo hacia afuera y la contraseña SMTP es justo lo que lo " +
        "haría posible. Deje la contraseña vacía, o ponga Smtp__AllowExternalRelay en true si " +
        "este entorno sí debe mandar.";

    /// <summary>
    /// Si la configuración puede arrancar. Falla únicamente en la combinación peligrosa: sin
    /// permiso de salida y con credencial.
    /// </summary>
    public static bool IsAllowed(SmtpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.AllowExternalRelay || !settings.HasCredentials;
    }
}
