namespace Sorteo360.Convocatorias.Web.ContactCampaignEndpoints;

/// <summary>
/// Textos del documento OpenAPI para la lectura de campañas por contacto. No se nombra ningún estado
/// HTTP ni ningún rol: salen de <c>.Produces</c> y de la política.
/// </summary>
internal static class ContactCampaignEndpointsDescriptions
{
    public const string ListSummary = "Lista las campañas enviadas a unos contactos del CRM";

    public const string ListDescription =
        """
        Una fila por contacto y campaña lanzada que lo tuvo en su lista. Un borrador no aparece: a
        nadie se le escribió todavía.

        Se piden de 1 a 100 contactos repitiendo `contactId`. Un contacto sin campañas no tiene filas;
        un id que el CRM no conoce tampoco, porque este servicio no consulta el CRM.

        `launchedAt` es cuándo se lanzó la campaña, la fecha del envío. `sendStatus` es el del
        destinatario: `pending`, `handed_off`, `failed` o `suppressed`. `registered` dice si hay una
        inscripción en el evento con la dirección a la que se escribió, y es nulo en un mensaje
        directo, que no tiene evento ni en qué inscribirse; ese trae su `subject`. El nombre del
        evento no viaja: se lee de events-service por `eventId`. Orden: lo más reciente primero.
        """;
}
