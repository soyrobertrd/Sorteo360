namespace Sorteo360.Convocatorias.Web.ReconciliationEndpoints;

/// <summary>
/// Textos del documento OpenAPI para la reconciliación. No se nombra ningún estado HTTP ni ningún
/// rol: salen de <c>.Produces</c> y de la política, y repetirlos acá los desincronizaría.
/// </summary>
internal static class ReconciliationEndpointsDescriptions
{
    public const string CountsSummary = "Cuenta lo que espera a una persona";

    public const string CountsDescription =
        """
        Los cinco cubos de la reconciliación, contados en todo el servicio. Nada acá se reintenta
        solo: esta es la pantalla que pone delante lo que quedó a medias.

        `uncancelledNotices` es nulo cuando events-service no contesta, porque es el único que sabe
        qué eventos se cancelaron. Los otros cuatro siguen contando.
        """;

    public const string PendingRegistrationSyncSummary = "Lista las inscripciones pendientes de sincronizar con el CRM";

    public const string DetailsDifferSummary = "Lista las inscripciones cuyos datos no coinciden con el CRM";

    public const string AcknowledgeDetailsSummary = "Da por revisada una inscripción cuyos datos no coinciden";

    public const string AcknowledgeDetailsDescription =
        """
        Sella quién la revisó y cuándo, y la saca del cubo `details-differ`. No toca el CRM: el
        arreglo, si hace falta, es editar el contacto allá. Apretarlo dos veces conserva el primer
        sello. Sobre una inscripción cuyos datos coinciden contesta `REGISTRATION_DETAILS_DO_NOT_DIFFER`.
        """;

    public const string PendingUnsubscribesSummary = "Lista las bajas que el CRM todavía no recibió";

    public const string ExclusionsSummary = "Lista a quién dejó fuera la selección de una campaña";

    public const string ExclusionsDescription =
        """
        Contesta "seleccioné 240 y veo 227, ¿quién falta?": la copia del nombre de cada persona, el
        código del motivo (`no_email`, `opted_out`, `do_not_invite`, `contact_unavailable`) y cuándo
        quedó fuera. No están en el listado de participantes porque nunca llegaron a ser destinatarios.
        """;

    public const string StrandedCampaignsSummary = "Lista las campañas que quedaron a medio entregar";

    public const string StrandedCampaignsDescription =
        """
        Campañas en `sending` con alguna fila `pending`, lanzadas hace más que el umbral configurado.
        Es lo que deja un redespliegue a mitad de envío. Cada fila trae el reparto de destinatarios
        por estado; `resume-handoff` manda la cola sin entregar y `retry-failed` los rechazados.
        Una campaña con su entrega viva puede asomar un momento: se va al refrescar.
        """;

    public const string UncancelledNoticesSummary = "Lista los eventos cancelados con inscritos sin avisar";

    public const string UncancelledNoticesDescription =
        """
        Un evento cancelado cuyos inscritos no recibieron el aviso: el diálogo de cancelar no llegó
        a hacer su segunda llamada. Cada fila trae el evento, cuándo se canceló y cuántos faltan; la
        acción es volver a correr `cancellation-notice`, que no le escribe a nadie dos veces. Qué
        eventos están cancelados lo pregunta a events-service, así que con él caído no se puede listar.
        """;
}
