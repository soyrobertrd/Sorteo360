namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Por qué un asistente no entra al bombo. Los siete, en el orden en que se evalúan (spec 01 §6):
/// gana el primero que aplica, así que nunca se pisan y la puerta cuadra con calificados más no
/// calificados. El valor es el mismo en el JSON y, desde PGTI-118, en la fila congelada.
/// </summary>
public static class ExclusionReason
{
    /// <summary>Se registró sin invitación. No se puede revertir a mano.</summary>
    public const string WalkIn = "walk_in";

    /// <summary>No tiene contacto en el CRM, así que no se le puede preguntar a la lista negra.</summary>
    public const string ContactUnresolved = "contact_unresolved";

    /// <summary>
    /// El CRM lo tiene suspendido: MITUR no hace negocios con él. Va antes que
    /// <see cref="DoNotInvite"/> porque la suspensión lo incluye. No se puede revertir a mano.
    /// </summary>
    public const string Suspended = "suspended";

    /// <summary>El CRM lo tiene marcado para no invitar. No se puede revertir a mano.</summary>
    public const string DoNotInvite = "do_not_invite";

    /// <summary>Alguien lo sacó a mano.</summary>
    public const string ManuallyExcluded = "manually_excluded";

    /// <summary>Hay una lista de DO Travel Awards en vigor y su correo no está.</summary>
    public const string NotAwardsMember = "not_awards_member";

    /// <summary>Su correo termina en <c>.do</c>.</summary>
    public const string DominicanDomain = "dominican_domain";

    /// <summary>Los siete en el orden en que se evalúan, que es también el orden en que se informan.</summary>
    public static readonly IReadOnlyList<string> InEvaluationOrder =
    [
        WalkIn, ContactUnresolved, Suspended, DoNotInvite, ManuallyExcluded, NotAwardsMember, DominicanDomain,
    ];
}
