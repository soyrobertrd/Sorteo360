namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>
/// Textos del documento OpenAPI para la ruta de baja. No se nombra ningún estado HTTP ni ningún
/// rol: los estados salen de <c>.Produces</c> y no se repiten acá para no desincronizarse.
/// </summary>
internal static class PublicUnsubscribeEndpointsDescriptions
{
    public const string UnsubscribeSummary = "Da de baja a quien lleva el token";

    public const string UnsubscribeDescription =
        """
        Da de baja al contacto cuyo token de baja llega en el cuerpo y devuelve su dirección
        enmascarada, como `a***@acme.com`.

        Es un `POST` y no un `GET` a propósito: el clic del enlace solo abre la página de
        confirmación, y el botón de esa página es quien hace este `POST`. Así un escáner
        corporativo que precarga cada enlace de un correo no da de baja a una lista de invitados
        en silencio.

        La baja se empuja al CRM, que es donde vive el estado. Si el CRM no está, la baja igual
        queda registrada y el próximo lanzamiento la termina de sincronizar, así que la persona
        nunca ve un fallo por un servicio del que no oyó hablar.

        Un token que no casa ninguna fila se rechaza sin decir más, para no confirmarle a nadie
        qué direcciones fueron invitadas.
        """;
}
