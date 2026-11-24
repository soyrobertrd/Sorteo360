namespace Sorteo360.Convocatorias.Core.Participants;

/// <summary>Cómo llegó una persona a la lista del evento (spec <c>07</c> §3.2).</summary>
public static class ParticipantOrigin
{
    /// <summary>Alguna campaña del evento la materializó con esta dirección.</summary>
    public const string Invited = "invited";

    /// <summary>Se inscribió con el enlace que recibió otra persona.</summary>
    public const string Referred = "referred";

    /// <summary>Se inscribió desde la cartelera, sin ningún enlace.</summary>
    public const string WalkIn = "walk_in";

    /// <summary>
    /// La regla, una sola vez: la usan la unión de participantes y la lista de check-ins de la rifa
    /// (spec <c>09</c> §7.1), que no la pueden copiar. Invitada gana a reenviada: quien tiene su
    /// propia invitación no se le atribuye a nadie. Lo que pasó con el envío no cuenta.
    /// </summary>
    /// <param name="isRecipient">
    /// Su dirección normalizada es la de un destinatario de alguna campaña lanzada del evento.
    /// </param>
    /// <param name="cameThroughLink">Su inscripción trae el enlace de un destinatario.</param>
    public static string Of(bool isRecipient, bool cameThroughLink) =>
        isRecipient ? Invited
        : cameThroughLink ? Referred
        : WalkIn;
}
