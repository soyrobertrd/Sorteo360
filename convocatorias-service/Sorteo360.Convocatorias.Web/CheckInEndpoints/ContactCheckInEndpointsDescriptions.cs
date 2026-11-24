namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// Textos del documento OpenAPI para la lectura por contacto. No se nombra ningún estado HTTP ni
/// ningún rol: salen de <c>.Produces</c> y de la política.
/// </summary>
internal static class ContactCheckInEndpointsDescriptions
{
    public const string ListSummary = "Lista los eventos a los que asistieron unos contactos del CRM";

    public const string ListDescription =
        """
        Una fila por contacto y evento en el que alguna inscripción suya tiene check-in. Estar
        invitado o inscrito no cuenta: cuenta haber entrado. Quien entró sin ser contacto del CRM no
        aparece para nadie, y a quien reenvió una invitación no le cuenta que su colega viniera.

        Se piden de 1 a 100 contactos repitiendo `contactId`. Un contacto sin asistencias no tiene
        filas; un id que el CRM no conoce tampoco, porque este servicio no consulta el CRM.

        `checkedInAt` es cuándo entró, y con eso la fecha del evento en que estuvo. El nombre y el
        lugar del evento no viajan: se leen de events-service por `eventId`. Orden: lo más reciente
        primero.
        """;
}
