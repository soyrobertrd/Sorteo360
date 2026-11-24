namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// Textos del documento OpenAPI para el escaneo y su deshecha. No se nombra ningún estado HTTP ni
/// ningún rol: salen de <c>.Produces</c> y de la política.
/// </summary>
internal static class CheckInWriteEndpointsDescriptions
{
    public const string CreateSummary = "Hace el check-in de una entrada en la puerta del evento";

    public const string CreateDescription =
        """
        Recibe el código QR pelado de la entrada; quitarle la URL alrededor es trabajo del escáner.
        El evento va en la ruta porque una terminal atiende un solo evento: una entrada de otro se
        rechaza y no se atiende en silencio, y el rechazo no nombra a nadie.

        Es idempotente. El primer escaneo hace el check-in; los siguientes devuelven el mismo
        cuerpo con `alreadyCheckedIn` verdadero y el `checkedInAt` del primero, para poder decir
        "entró a las 18:42". Dos terminales a la vez hacen un solo check-in.

        No consulta a events-service: un evento cancelado o cerrado no frena la puerta. La
        respuesta no lleva el correo de la persona.
        """;

    public const string CreateByRegistrationSummary = "Hace el check-in de una inscripción elegida en la lista de acceso";

    public const string CreateByRegistrationDescription =
        """
        Para quien no tiene un QR que mostrar: el personal de la puerta lo busca en la lista de
        acceso, mira un documento y lo hace pasar por el id de su inscripción. Es otra forma de
        identificar la inscripción, no otra clase de check-in: responde igual que el escaneo, con el
        mismo cuerpo, la misma idempotencia y el mismo `alreadyCheckedIn`.

        Una inscripción de otro evento se rechaza sin nombrar a nadie. No consulta a events-service,
        y la respuesta no lleva el correo de la persona.
        """;

    public const string UndoSummary = "Deshace el check-in de una inscripción del evento";

    public const string UndoDescription =
        """
        Borra el check-in, así que un escaneo posterior hace uno nuevo. Sale bien haya habido
        check-in o no, para que un doble toque no sea un error. Quién hizo el check-in borrado y
        quién lo deshizo quedan en el log del servicio, no en la base.

        No alcanza a un sorteo ya empezado: el sorteo congela su lista al empezar.
        """;
}
