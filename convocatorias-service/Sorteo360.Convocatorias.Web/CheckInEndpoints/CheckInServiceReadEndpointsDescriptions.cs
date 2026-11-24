namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// Textos del documento OpenAPI para la lista de check-ins de la rifa. No se nombra ningún estado HTTP
/// ni ningún rol: salen de <c>.Produces</c> y de la política.
/// </summary>
internal static class CheckInServiceReadEndpointsDescriptions
{
    public const string ListSummary = "Lista quiénes hicieron check-in en el evento, para la rifa";

    public const string ListDescription =
        """
        Todas las inscripciones del evento con check-in, sin filtrar y sin paginar, ordenadas por
        `checkedInAt` y después por `registrationId`. La lee events-service para armar el bombo de la
        rifa; clasificar a cada persona es trabajo de la rifa, no de esta lista.

        `origin` es la misma procedencia de la lista de participantes: `invited`, `referred` o
        `walk_in`. `contactId` es el contacto del CRM de quien se inscribió, nunca el de quien le
        reenvió la invitación, y es nulo mientras su escritura al CRM está pendiente.

        Un evento sin check-ins, o que este servicio no conoce, devuelve `items` vacío.
        """;
}
