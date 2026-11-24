namespace Sorteo360.Convocatorias.Web.CancellationNoticeEndpoints;

/// <summary>
/// Textos del documento OpenAPI para el aviso de cancelación. No se nombra ningún estado HTTP ni
/// ningún rol: salen de <c>.Produces</c> y de la política, y repetirlos acá los desincronizaría.
/// </summary>
internal static class CancellationNoticeEndpointsDescriptions
{
    public const string NotifySummary = "Avisa a los inscritos que el evento se canceló";

    public const string NotifyDescription =
        """
        Le manda un aviso genérico de cancelación a cada persona inscrita en el evento que todavía
        no lo recibió, con su referencia y sin el motivo de la cancelación. Se llama justo después
        de cancelar el evento en events-service.

        Se niega con `EVENT_NOT_CANCELLED` si el evento no está cancelado, incluido uno cerrado, y
        no le escribe a nadie. Con events-service caído contesta `EVENTS_UNAVAILABLE` y tampoco.

        Llamarla dos veces no le escribe a nadie dos veces: la segunda solo intenta a quien la
        primera no alcanzó. La respuesta dice a cuántos les salió el correo y a cuántos no.
        """;
}
