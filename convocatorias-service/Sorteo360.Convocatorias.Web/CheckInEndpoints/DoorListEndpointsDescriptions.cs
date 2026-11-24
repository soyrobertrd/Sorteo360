namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// Textos del documento OpenAPI para la lista de acceso. No se nombra ningún estado HTTP ni ningún
/// rol: salen de <c>.Produces</c> y de la política.
/// </summary>
internal static class DoorListEndpointsDescriptions
{
    public const string ListSummary = "Lista las inscripciones del evento para la puerta";

    public const string ListDescription =
        """
        Para quien no tiene un QR que mostrar: se lo busca acá, se mira un documento y se le hace el
        check-in por el id de su inscripción. Ordenada por apellido, nombre y referencia.

        `search` se parte en palabras y cada una tiene que aparecer en el nombre, el apellido o la
        referencia, sin importar mayúsculas ni tildes. Además, lo tecleado entero se compara con la
        dirección de correo, exacta: un pedazo de dirección no encuentra a nadie. El correo se puede
        buscar pero nunca se devuelve.

        `totals` es el del evento entero y no cambia con la búsqueda ni con `checkedIn`. No es la
        lista de participantes: no tiene invitados sin inscripción, ni estado de envío, ni correos.
        """;
}
