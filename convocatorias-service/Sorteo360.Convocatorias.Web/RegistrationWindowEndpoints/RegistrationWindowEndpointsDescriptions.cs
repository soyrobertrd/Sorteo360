namespace Sorteo360.Convocatorias.Web.RegistrationWindowEndpoints;

/// <summary>
/// Textos del documento OpenAPI para la ventana de inscripción. No se nombra ningún estado HTTP ni
/// ningún rol: salen de <c>.Produces</c> y de la política, y repetirlos acá los desincronizaría.
/// </summary>
internal static class RegistrationWindowEndpointsDescriptions
{
    public const string CloseRegistrationsSummary = "Cierra las inscripciones de un evento";

    public const string CloseRegistrationsDescription =
        """
        Deja de aceptar inscripciones nuevas para el evento. El evento sigue en pie y en la
        cartelera pública con `registrationOpen: false`, toda inscripción que ya existe se
        mantiene con su entrada y su QR, y no se le manda nada a nadie.

        Resolver un enlace e inscribirse contestan `REGISTRATIONS_CLOSED` mientras siga cerrada,
        y lanzar una campaña del evento se niega hasta que se reabra.

        Cerrar una ventana ya cerrada no cambia nada: conserva la hora y la persona del primer
        cierre.

        No confundir con cerrar el evento, que significa que ya ocurrió.
        """;

    public const string ReopenRegistrationsSummary = "Reabre las inscripciones de un evento";

    public const string ReopenRegistrationsDescription =
        """
        Vuelve a aceptar inscripciones nuevas y sella quién y cuándo reabrió. Reabrir una ventana
        abierta no cambia nada.
        """;
}
