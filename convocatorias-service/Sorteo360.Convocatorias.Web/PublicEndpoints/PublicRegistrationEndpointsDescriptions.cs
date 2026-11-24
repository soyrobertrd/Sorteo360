namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>
/// Textos del documento OpenAPI para las dos rutas del portal de registro.
/// <para>
/// No se nombra ningún estado HTTP ni ningún rol: los estados salen de <c>.Produces</c> y
/// <c>.ProducesProblem</c>, y el rol lo dibuja el transformer desde la política del endpoint, así
/// que repetirlos acá es exactamente lo que después se desincroniza.
/// </para>
/// </summary>
internal static class PublicRegistrationEndpointsDescriptions
{
    public const string ResolveSummary = "Resuelve un enlace de invitación";

    public const string ResolveDescription =
        """
        Devuelve el evento al que apunta el token del enlace.

        Es un `POST` y no un `GET` a propósito: el token viaja en el cuerpo, porque nginx y
        Traefik anotan la línea de petición entera en su registro de acceso y una cadena de
        consulta lo dejaría escrito en disco.

        `prefill` solo viene en un evento **privado**, cuyo token está atado a una sola persona y
        llegó a su dirección. El token de un evento público está pensado para reenviarse, así que
        rellenar sobre él le enseñaría a quien recibe el reenvío el nombre y la dirección del
        invitado original.

        Un token que no existe, uno ya usado en un evento privado, y un evento cancelado, cerrado
        o ya terminado se rechazan todos igual y con el mismo código: decir cuál de ellos es le
        confirmaría a un desconocido qué direcciones están invitadas.

        Con las inscripciones del evento cerradas contesta `REGISTRATIONS_CLOSED`, también con un
        token privado: el evento sigue en la cartelera, así que esa negativa sí dice su motivo.
        """;

    public const string RegisterSummary = "Inscribe a una persona en un evento";

    public const string RegisterDescription =
        """
        Devuelve el `reference` que se imprime en la escarapela y el `qrCode` con el que se pide
        la entrada.

        `token` es **opcional**. Con él se valida la invitación y la inscripción queda atada al
        destinatario que la trajo, que es lo que vuelve representable un reenvío. Sin él se acepta
        solo si el evento es público, y es el camino de quien llega desde la cartelera sin haber
        recibido ningún correo.

        **Mandar el mismo formulario dos veces no es un error**: la segunda vez devuelve la
        inscripción que ya había con sus mismos códigos, no crea ninguna fila y no pisa lo que la
        persona escribió la primera vez. Una persona por evento, no por campaña.

        Un token inválido o ya usado, y un evento desconocido, privado sin token, cancelado,
        cerrado o ya terminado se rechazan con el mismo código que `resolve`.

        Con las inscripciones del evento cerradas, una persona nueva recibe `REGISTRATIONS_CLOSED`.
        Quien ya estaba inscrito y reenvía su formulario sigue recibiendo su inscripción.
        """;

    public const string TicketSummary = "Devuelve la entrada de un `qrCode`";

    public const string TicketDescription =
        """
        La entrada que lleva ese `qrCode`: la referencia impresa, el evento, el nombre y la
        empresa. Nada más: ni datos del CRM, ni la dirección de correo, ni quién trajo la
        inscripción.

        El `qrCode` va en la ruta, no en el cuerpo: es lo que codifica el QR y un secreto de menor
        valor que el token del enlace.

        **Sigue sirviendo en un evento cancelado o cerrado**: quien ya tiene su entrada puede
        mirarla. Un `qrCode` que no existe devuelve `404`, el mismo código que las demás rutas del
        portal.
        """;

    public const string QrSummary = "Devuelve la imagen PNG del QR de un `qrCode`";

    public const string QrDescription =
        """
        La imagen `image/png` del código QR, **generada al vuelo**: no se escribe en disco y no
        pide ningún volumen.

        El QR codifica la URL de la página de la entrada, así que un escáner o un teléfono la
        abren al leerlo.

        No lee el evento, de modo que **sigue sirviendo aunque el evento esté cancelado o
        cerrado**, y también si events-service está caído. Un `qrCode` que no existe devuelve
        `404`.
        """;
}
