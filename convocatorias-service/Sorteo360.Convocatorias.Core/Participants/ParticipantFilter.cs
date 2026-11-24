using System.Globalization;

namespace Sorteo360.Convocatorias.Core.Participants;

/// <summary>
/// Los cinco filtros de la lista de participantes (spec <c>07</c> §3.3). Los mismos cinco sirven
/// al CSV (§4), así que viven acá y no en el endpoint.
/// <para>
/// Las tres preguntas del cliente son tres combinaciones de esto: <c>origin=invited</c> para a
/// quién invitamos, <c>registered=true</c> para quién se inscribió, y
/// <c>origin=invited&amp;registered=false</c> para quién no.
/// </para>
/// </summary>
/// <param name="Origin">Uno de <see cref="ParticipantOrigin"/>.</param>
/// <param name="SendStatus">Uno de los estados de entrega, o <see cref="ParticipantSendStatus.None"/>.</param>
/// <param name="Registered">Solo quien se inscribió, o solo quien no.</param>
/// <param name="Search">Subcadena sobre el nombre y la dirección, sin mayúsculas ni tildes.</param>
/// <param name="CampaignId">
/// Solo los destinatarios de esa campaña, borrador incluido. Además de filtrar cambia qué se lee:
/// los destinatarios de un borrador no entran en ninguna otra lectura (§2), así que con este filtro
/// se leen los de esa campaña y solo esos, y el estado de entrega de la fila es el de esa campaña.
/// </param>
public sealed record ParticipantFilter(
    string? Origin = null,
    string? SendStatus = null,
    bool? Registered = null,
    string? Search = null,
    Guid? CampaignId = null)
{
    /// <summary>Sin filtro: el evento entero, sin borradores.</summary>
    public static ParticipantFilter None { get; } = new();

    private const CompareOptions SearchComparison = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// Si la fila pasa los cinco filtros. Un valor fuera del catálogo no
    /// devuelve todo sino nada, como el filtro de estado de las campañas: colar un filtro que no se
    /// entiende haría creer que la lista está completa.
    /// </summary>
    public bool Matches(Participant participant)
    {
        if (!string.IsNullOrWhiteSpace(Origin) && participant.Origin != Origin.Trim())
            return false;

        if (!string.IsNullOrWhiteSpace(SendStatus) && participant.SendStatus != SendStatus.Trim())
            return false;

        if (Registered is { } registered && participant.Registered != registered)
            return false;

        // Quien se inscribió sin estar entre los destinatarios de la campaña no es de su lista.
        if (CampaignId is { } campaignId && !participant.CampaignIds.Contains(campaignId))
            return false;

        return string.IsNullOrWhiteSpace(Search) || Contains(participant, Search.Trim());
    }

    /// <summary>Si la búsqueda aparece en la dirección o en el nombre completo.</summary>
    private static bool Contains(Participant participant, string search)
    {
        var text = CultureInfo.InvariantCulture.CompareInfo;

        return new[]
        {
            participant.Email,
            $"{participant.FirstName.Trim()} {participant.LastName.Trim()}",
        }.Any(field => text.IndexOf(field, search, SearchComparison) >= 0);
    }
}
