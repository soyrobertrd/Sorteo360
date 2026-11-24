namespace Sorteo360.Convocatorias.Web.ReconciliationEndpoints;

/// <summary>
/// Textos del documento OpenAPI para los dos reintentos de reconciliación. No se nombra ningún
/// estado HTTP ni ningún rol: salen de <c>.Produces</c> y de la política.
/// </summary>
internal static class ReconciliationSyncEndpointsDescriptions
{
    public const string CrmSyncSummary = "Reintenta llevar una inscripción al CRM";

    public const string CrmSyncDescription =
        """
        Lleva al CRM una inscripción que quedó pendiente porque el CRM no contestó, por el mismo
        camino que la inscripción pública: crea el contacto con su consentimiento, o enlaza el que
        ya ocupa la dirección sin tocarlo, y marca `contactDetailsDiffer` si el nombre no coincide.

        `canBeEmailed` es lo que el CRM dice de esa ficha. Se muestra y no se guarda: una persona
        suprimida puede inscribirse igual.

        Una inscripción ya sincronizada vuelve tal cual, sin llamar al CRM. Si el CRM sigue sin
        contestar, `CRM_UNAVAILABLE` y la inscripción sigue pendiente.
        """;

    public const string UnsubscribeSyncSummary = "Reintenta llevar al CRM la baja de un destinatario";

    public const string UnsubscribeSyncDescription =
        """
        Empuja al CRM una baja que quedó pendiente porque el CRM no contestó, por el mismo camino
        que la baja pública, y la sella como sincronizada. La hora del clic no cambia.

        Una baja ya sincronizada vuelve tal cual, sin llamar al CRM. Si el CRM sigue sin contestar,
        `CRM_UNAVAILABLE` y la baja sigue pendiente. Un destinatario que nunca se dio de baja
        contesta `RECIPIENT_NOT_UNSUBSCRIBED`: esta ruta no da de baja a nadie.
        """;
}
