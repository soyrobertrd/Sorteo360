namespace Sorteo360.Convocatorias.Web.TrackingEndpoints;

/// <summary>
/// Textos del documento OpenAPI para el resumen del evento. No se nombra ningún estado HTTP ni ningún
/// rol: salen de <c>.Produces</c> y de la política, y repetirlos acá los desincronizaría.
/// </summary>
internal static class TrackingEndpointsDescriptions
{
    public const string GetSummary = "Resume un evento: invitados, inscritos, quién falta y qué pide atención";

    public const string GetDescription =
        """
        Las campañas del evento, sin borradores, y los conteos de la lista de participantes: cada
        persona cuenta una vez por muchas campañas que la hayan alcanzado. `invited` es entregado al
        proveedor; `notRegistered` son esas personas sin inscripción, nunca entradas libres.

        `needsAttention` suma las inscripciones pendientes de sincronizar, las fichas que no
        coinciden sin revisar y las campañas varadas de este evento. La ventana de inscripción viene
        siempre y no cuenta como atención.

        No consulta a events-service ni al CRM: un evento que este servicio no conoce devuelve el
        resumen en cero con la ventana abierta.
        """;
}
