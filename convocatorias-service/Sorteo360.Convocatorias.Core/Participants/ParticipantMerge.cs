using System.Globalization;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Shared;

namespace Sorteo360.Convocatorias.Core.Participants;

/// <summary>
/// La unión de destinatarios e inscripciones de un evento en una fila por persona (spec <c>07</c>
/// §3). La usan la lista, el resumen y el CSV, así que vive una vez y acá.
/// </summary>
public static class ParticipantMerge
{
    /// <summary>
    /// De varias campañas sale un solo estado, el que más cerca dejó a la persona del correo: una
    /// vez entregada, lo que pasó en otra campaña no cambia que le escribimos. <c>pending</c> va
    /// antes que los dos fallos porque todavía puede salir.
    /// </summary>
    private static readonly string[] SendStatusPrecedence =
    [
        RecipientSendStatus.HandedOff,
        RecipientSendStatus.Pending,
        RecipientSendStatus.Failed,
        RecipientSendStatus.Suppressed,
    ];

    /// <summary>
    /// Mayúsculas y tildes aparte, con la cultura invariante, como <see cref="PersonNameShape"/>:
    /// "Álvarez" va con las aes y no detrás de la zeta.
    /// </summary>
    private const CompareOptions NameOrder = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// Une las dos fuentes en una fila por dirección recortada y en minúsculas, y nada más (§3.1),
    /// ordenadas por apellido, nombre y dirección (§3.3).
    /// <para>
    /// <b>La dirección es la única clave.</b> El id de contacto no, porque una inscripción pendiente
    /// de sincronizar no lo tiene; y el nombre tampoco, porque dos personas se llaman igual. Quien
    /// fue invitado en una dirección y se inscribió en otra son dos filas, a propósito.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Participant> Merge(
        IEnumerable<RecipientSource> recipients,
        IEnumerable<RegistrationSource> registrations)
    {
        var invitedByKey = recipients.ToLookup(r => EmailAddressShape.Normalize(r.Email));
        var registrationByKey = registrations.ToDictionary(r => EmailAddressShape.Normalize(r.Email));

        var keys = invitedByKey.Select(g => g.Key).Union(registrationByKey.Keys);

        return [.. keys
            .Select(key => ToParticipant(
                [.. invitedByKey[key].OrderBy(r => r.CreatedAt)],
                registrationByKey.GetValueOrDefault(key)))
            .Order(Comparer<Participant>.Create(CompareForList))];
    }

    /// <summary>
    /// El orden de la lista. La dirección normalizada al final es lo que lo vuelve total —dos
    /// personas no comparten fila, así que no comparten clave—, y un orden total es lo que impide
    /// que la página dos repita una fila de la uno.
    /// </summary>
    private static int CompareForList(Participant left, Participant right)
    {
        var names = CultureInfo.InvariantCulture.CompareInfo;

        var byLastName = names.Compare(left.LastName, right.LastName, NameOrder);
        if (byLastName != 0)
            return byLastName;

        var byFirstName = names.Compare(left.FirstName, right.FirstName, NameOrder);
        if (byFirstName != 0)
            return byFirstName;

        return string.CompareOrdinal(EmailAddressShape.Normalize(left.Email), EmailAddressShape.Normalize(right.Email));
    }

    /// <summary>
    /// La fila de una persona: sus invitaciones, en el orden en que se materializaron, y su
    /// inscripción si la tiene. La procedencia es <see cref="ParticipantOrigin.Of"/>.
    /// <para>
    /// Una inscripción que trae un enlace y otra dirección es <c>referred</c>, aunque la haya hecho
    /// la misma persona invitada desde su correo personal. Los datos no distinguen ese caso de un
    /// reenvío, y por la regla de la clave (§3.1) otra dirección es otra persona.
    /// </para>
    /// </summary>
    private static Participant ToParticipant(IReadOnlyList<RecipientSource> invitations, RegistrationSource? registration)
    {
        var invited = invitations.Count > 0;
        var origin = ParticipantOrigin.Of(invited, cameThroughLink: registration?.ReferredByName is not null);

        // El nombre y la dirección que se ven son los que la persona escribió al inscribirse, y si
        // no se inscribió, la copia de su primera invitación. Lo que dice el CRM ya está al lado
        // como `detailsDiffer`, que es justo la marca que avisa cuando no coinciden.
        var name = registration is not null
            ? (registration.Email, registration.FirstName, registration.LastName)
            : (invitations[0].Email, invitations[0].FirstName, invitations[0].LastName);

        return new Participant(
            name.Email,
            name.FirstName,
            name.LastName,
            registration?.Company,
            origin,
            invited ? FurthestSendStatus(invitations) : ParticipantSendStatus.None,
            Registered: registration is not null,
            registration?.RegisteredAt,
            registration?.Reference,
            origin == ParticipantOrigin.Referred ? registration!.ReferredByName : null,
            [.. invitations.Select(r => r.CampaignId).Distinct()],
            registration?.CrmSyncPending ?? false,
            registration?.DetailsDiffer ?? false)
        {
            NotificationIds = invited
                ? [.. invitations.Where(r => r.NotificationId is not null).Select(r => r.NotificationId!.Value)]
                : [],
            CheckedInAt = registration?.CheckedInAt,
        };
    }

    /// <summary>El estado de entrega que más lejos llegó entre sus campañas.</summary>
    private static string FurthestSendStatus(IReadOnlyList<RecipientSource> invitations) =>
        SendStatusPrecedence.First(status => invitations.Any(r => r.SendStatus == status));
}
