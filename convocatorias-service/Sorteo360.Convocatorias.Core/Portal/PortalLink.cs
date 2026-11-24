namespace Sorteo360.Convocatorias.Core.Portal;

/// <summary>
/// Los dos enlaces que lleva una invitación, armados sobre <see cref="PortalSettings.BaseUrl"/>.
/// <para>
/// <b>El token va en el fragmento, nunca en la cadena de consulta</b> (spec <c>06</c> §2.1). El
/// navegador no manda el fragmento al servidor, así que el token no llega a ningún registro de
/// acceso: ni al de nginx, ni al de Traefik, ni al de la red del cliente. Por eso resolver es un
/// <c>POST</c> con el token en el cuerpo.
/// </para>
/// </summary>
public static class PortalLink
{
    /// <summary>Dónde aterriza quien acepta la invitación.</summary>
    private const string RegistrationPath = "/publico/registro";

    /// <summary>
    /// Dónde aterriza quien quiere dejar de recibirlas. El clic solo abre la página de
    /// confirmación; darse de baja es el <c>POST</c> del botón, para que un escáner corporativo
    /// que precarga enlaces no dé de baja a nadie en silencio (spec <c>06</c> §2.4).
    /// <para>
    /// <b>La spec fija el fragmento y no la ruta</b> (<c>06</c> §2.4 solo dice <c>#u=&lt;token&gt;</c>),
    /// así que esta la elegimos acá. La pantalla del portal tiene que atender exactamente esta, y
    /// cambiarla después rompe todo enlace ya enviado.
    /// </para>
    /// </summary>
    private const string UnsubscribePath = "/publico/baja";

    /// <summary>
    /// Dónde ve alguien su entrada. <b>El <c>qr_code</c> va en la ruta, no en el fragmento</b>, al
    /// revés que los otros dos: es lo que codifica el QR, y para que un escáner en la puerta lo lea
    /// el servidor tiene que verlo, cosa que un fragmento no le da (spec <c>06</c> §2.3). Que el
    /// <c>qr_code</c> aparezca en los registros de acceso es un costo ya aceptado —un
    /// <c>&lt;img src&gt;</c> en un correo obliga a una URL que se pueda pedir con un GET— y es un
    /// secreto de menor valor que el token.
    /// </summary>
    private const string TicketPath = "/publico/entrada";

    /// <summary>
    /// El prefijo público del servicio, tal como lo resuelve la versión 1. La imagen del QR la
    /// sirve este servicio, no el portal, así que su URL cuelga de acá y no de una ruta del
    /// portal —aunque el origen sea el mismo a través de nginx (spec <c>00</c> §4).
    /// </summary>
    private const string PublicApiPath = "/v1/public/registrations";

    /// <summary>El enlace de registro de una persona: <c>#t=&lt;token&gt;</c>.</summary>
    public static string Registration(string baseUrl, string token) =>
        Build(baseUrl, RegistrationPath, "t", token);

    /// <summary>El enlace de baja de una persona: <c>#u=&lt;token&gt;</c>.</summary>
    public static string Unsubscribe(string baseUrl, string token) =>
        Build(baseUrl, UnsubscribePath, "u", token);

    /// <summary>
    /// La página de la entrada de una persona: <c>/publico/entrada/&lt;qr_code&gt;</c>. Es lo que
    /// codifica el QR, así que el código va en la ruta y no en el fragmento.
    /// </summary>
    public static string Ticket(string baseUrl, string qrCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(qrCode);

        return $"{baseUrl.TrimEnd('/')}{TicketPath}/{qrCode}";
    }

    /// <summary>
    /// La URL de la imagen del QR de una persona: <c>/v1/public/registrations/&lt;qr_code&gt;/qr</c>.
    /// Es lo que va en <c>{{qr}}</c> del correo de confirmación, un <c>&lt;img src&gt;</c> que un
    /// cliente de correo pide con un GET, así que tiene que ser una URL absoluta y alcanzable
    /// (spec <c>06</c> §4.4 y §4.5). La sirve este servicio, no el portal, pero el origen es el
    /// mismo a través de nginx, así que se arma sobre la misma base.
    /// </summary>
    public static string QrImage(string baseUrl, string qrCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(qrCode);

        return $"{baseUrl.TrimEnd('/')}{PublicApiPath}/{qrCode}/qr";
    }

    /// <summary>
    /// Base, ruta y fragmento. La barra final de la base se recorta: la variable la escribe una
    /// persona en un compose y las dos formas son igual de razonables de escribir.
    /// </summary>
    private static string Build(string baseUrl, string path, string key, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return $"{baseUrl.TrimEnd('/')}{path}#{key}={token}";
    }
}
