using CSharpFunctionalExtensions;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// El pipeline que convierte la lista de la puerta en el bombo (spec 01 §2, §6). Es puro: no lee
/// nada ni mira la hora, así que da lo mismo en la lectura del borrador que al arrancar.
/// <para>
/// Gana el primer motivo que aplica. Los cuatro primeros —sin invitación, sin contacto en el CRM,
/// suspendido y vetado por el CRM— van antes que la decisión manual, así que una inclusión nunca
/// los vence y una exclusión queda tapada por ellos: la lista negra está por encima de la persona
/// (spec 01 §3).
/// </para>
/// </summary>
public static class Qualification
{
    /// <summary>
    /// El <c>inviteSuppressionReason</c> del CRM que se nombra como suspensión. Es un valor que el
    /// CRM publica, no el nombre de nuestro motivo, aunque se escriban igual. Cualquier otro motivo
    /// por el que no se puede invitar —<c>do_not_invite</c> o uno que el CRM agregue mañana— se
    /// nombra <c>do_not_invite</c>: quien decide es <c>canBeInvited</c>, no el nombre (spec 01 §3).
    /// </summary>
    private const string SuspendedInvitation = "suspended";

    /// <summary>
    /// Clasifica a cada asistente, en el mismo orden en que llegaron.
    /// <para>
    /// Con <paramref name="awardsList"/> en <c>None</c> el paso de DO Travel Awards se salta entero.
    /// Con <paramref name="mailability"/> en <c>None</c> —el CRM no contestó— se salta el de la lista
    /// negra, y nada más: quien llama lo informa y bloquea el arranque. Con respuesta, un contacto que
    /// no viene en ella queda en <c>contact_unresolved</c>.
    /// </para>
    /// </summary>
    /// <param name="attendees">La lista de la puerta.</param>
    /// <param name="awardsList">La lista de DO Travel Awards en vigor, si la hay.</param>
    /// <param name="overrides">Las decisiones manuales del sorteo, por correo normalizado.</param>
    /// <param name="mailability">
    /// Por qué el CRM no deja invitar a cada contacto, por su id: <c>null</c> cuando se le puede invitar.
    /// </param>
    public static QualificationResult Classify(
        IReadOnlyList<Attendee> attendees,
        Maybe<AwardsList> awardsList,
        IReadOnlyCollection<AttendeeOverride> overrides,
        Maybe<IReadOnlyDictionary<Guid, string?>> mailability)
    {
        ArgumentNullException.ThrowIfNull(attendees);
        ArgumentNullException.ThrowIfNull(overrides);

        var overridesByEmail = overrides.ToDictionary(o => o.IdentityKey, StringComparer.Ordinal);

        return new QualificationResult(attendees
            .Select(a => ClassifyOne(a, awardsList, overridesByEmail.GetValueOrDefault(a.IdentityKey), mailability))
            .ToList());
    }

    /// <summary>
    /// Un asistente, en el orden de spec 01 §6. <c>OverruledReason</c> sigue la tabla de §4.1: lo
    /// que venció una inclusión, o lo que la venció a ella; nunca nada en una exclusión.
    /// </summary>
    private static QualifiedAttendee ClassifyOne(
        Attendee attendee,
        Maybe<AwardsList> awardsList,
        AttendeeOverride? manual,
        Maybe<IReadOnlyDictionary<Guid, string?>> mailability)
    {
        var included = manual?.Decision == AttendeeOverrideDecision.Included;

        var unoverridable = UnoverridableExclusion(attendee, mailability);
        if (unoverridable is not null)
            return new QualifiedAttendee(attendee, unoverridable, manual, included ? unoverridable : null);

        if (manual?.Decision == AttendeeOverrideDecision.Excluded)
            return new QualifiedAttendee(attendee, ExclusionReason.ManuallyExcluded, manual, OverruledReason: null);

        var overridable = OverridableExclusion(attendee, awardsList);
        return included
            ? new QualifiedAttendee(attendee, ExclusionReason: null, manual, overridable)
            : new QualifiedAttendee(attendee, overridable, manual, OverruledReason: null);
    }

    /// <summary>
    /// Reglas corporativas sin filtro de CRM ni dominio <c>.do</c>: los asistentes importados al
    /// padrón o agregados en puerta califican salvo que el CRM responda explícitamente con veto.
    /// </summary>
    private static string? UnoverridableExclusion(Attendee attendee, Maybe<IReadOnlyDictionary<Guid, string?>> mailability)
    {
        if (attendee.ContactId is not { } contactId || mailability.HasNoValue)
            return null;

        if (!mailability.Value.TryGetValue(contactId, out var inviteSuppressionReason))
            return null;

        return inviteSuppressionReason switch
        {
            null => null,
            SuspendedInvitation => ExclusionReason.Suspended,
            _ => ExclusionReason.DoNotInvite,
        };
    }

    /// <summary>Exclusión superable por inclusión manual (lista de membresía cuando aplica).</summary>
    private static string? OverridableExclusion(Attendee attendee, Maybe<AwardsList> awardsList)
    {
        if (awardsList.HasValue && !awardsList.Value.Members.Contains(attendee.IdentityKey))
            return ExclusionReason.NotAwardsMember;

        return null;
    }
}

/// <summary>
/// Un asistente con su clasificación: calificado, o el único motivo por el que no.
/// <para>
/// <paramref name="Override"/> es la decisión manual que lo nombra, haya ganado o no.
/// <paramref name="OverruledReason"/> es lo que esa decisión venció —o lo que la venció a ella—, y
/// es nulo sin decisión, en una exclusión, y en una inclusión sobre alguien que calificaba igual.
/// </para>
/// </summary>
public sealed record QualifiedAttendee(
    Attendee Attendee,
    string? ExclusionReason,
    AttendeeOverride? Override,
    string? OverruledReason)
{
    public bool Qualified => ExclusionReason is null;
}

/// <summary>
/// La lista de la puerta clasificada. Las cuentas salen de la misma lista, así que
/// <see cref="CheckedIn"/> siempre es <see cref="Qualified"/> más <see cref="NotQualified"/>.
/// </summary>
public sealed record QualificationResult(IReadOnlyList<QualifiedAttendee> Attendees)
{
    public int CheckedIn => Attendees.Count;
    public int Qualified => Attendees.Count(a => a.Qualified);
    public int NotQualified => CheckedIn - Qualified;
}
