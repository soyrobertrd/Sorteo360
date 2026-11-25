using System.Text.RegularExpressions;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// La parte pura de la exclusión en lote: leer los identificadores y decidir, contra el padrón y la
/// puerta del evento destino, a quién se excluye y qué se informa. No toca la base.
/// </summary>
public static partial class BulkExclusion
{
    public const int MaxIdentifiers = 5000;

    /// <summary>
    /// A quién buscar en el destino: su correo, su ID, o los dos (un ganador de otro evento). Con uno
    /// que coincida basta. <see cref="Label"/> es lo que se informa si no se lo encuentra;
    /// <see cref="FromFile"/> marca un valor suelto de un archivo, no un ganador de otro evento.
    /// </summary>
    public sealed record Candidate(string Label, string? Email, string? Id, bool FromFile = false);

    /// <summary>
    /// Los identificadores de un archivo: recortados, sin vacíos ni repetidos (sin distinguir
    /// mayúsculas). Con <c>@</c> es un correo; si no, un ID. Un correo mal formado va a inválidos.
    /// </summary>
    public static (IReadOnlyList<Candidate> Candidates, IReadOnlyList<string> Invalid) ReadIdentifiers(IEnumerable<string?> identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<Candidate>();
        var invalid = new List<string>();
        foreach (var raw in identifiers)
        {
            var value = raw?.Trim();
            if (string.IsNullOrEmpty(value) || !seen.Add(value))
                continue;

            if (!value.Contains('@'))
                candidates.Add(new Candidate(value, Email: null, Id: PersonIdentity.NormalizeId(value), FromFile: true));
            else if (EmailShape().IsMatch(value))
                candidates.Add(new Candidate(value, Email: PersonIdentity.NormalizeEmail(value), Id: null, FromFile: true));
            else
                invalid.Add(value);
        }

        return (candidates, invalid);
    }

    /// <summary>
    /// Decide sobre cada candidato. Se lo busca primero por correo y después por ID, así un ganador
    /// es una sola persona del destino. Sin coincidencia: un ID suelto de archivo es inválido; lo
    /// demás no está en el padrón. Encontrado pero fuera de la puerta, tampoco. Dos candidatos que
    /// llevan a la misma persona cuentan una vez.
    /// </summary>
    /// <param name="roster">El padrón del destino que coincide con algún candidato.</param>
    /// <param name="doorKeys">Las claves de quienes están en la puerta del destino.</param>
    /// <param name="overrides">Las decisiones manuales que ya tiene el sorteo destino.</param>
    public static (BulkExcludeOutcome Outcome, IReadOnlyList<string> KeysToExclude) Classify(
        IReadOnlyList<Candidate> candidates,
        IReadOnlyList<string> invalid,
        IReadOnlyList<string> unidentifiable,
        IReadOnlyList<RosterPerson> roster,
        IReadOnlySet<string> doorKeys,
        IReadOnlyCollection<AttendeeOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(doorKeys);
        ArgumentNullException.ThrowIfNull(overrides);

        var byEmail = new Dictionary<string, RosterPerson>(StringComparer.Ordinal);
        var byId = new Dictionary<string, RosterPerson>(StringComparer.Ordinal);
        foreach (var person in roster)
        {
            if (person.Email is { } email)
                byEmail.TryAdd(email, person);
            if (PersonIdentity.NormalizeId(person.EmployeeCode) is { } id)
                byId.TryAdd(id, person);
        }

        var decisions = overrides.ToDictionary(o => o.IdentityKey, o => o.Decision, StringComparer.Ordinal);
        var notInRoster = new List<string>();
        var invalidOut = new List<string>(invalid);
        var handled = new HashSet<string>(StringComparer.Ordinal);
        var toExclude = new List<string>();
        var alreadyExcluded = 0;

        foreach (var candidate in candidates)
        {
            RosterPerson? found = null;
            if (candidate.Email is { } email)
                byEmail.TryGetValue(email, out found);
            if (found is null && candidate.Id is { } id)
                byId.TryGetValue(id, out found);

            if (found is null)
            {
                // Un ID de archivo que nadie tiene no es una persona que falte: es un dato que no sirve.
                if (candidate.FromFile && candidate.Email is null)
                    invalidOut.Add(candidate.Label);
                else
                    notInRoster.Add(candidate.Label);
                continue;
            }

            if (!handled.Add(found.IdentityKey))
                continue;

            if (!doorKeys.Contains(found.IdentityKey))
            {
                notInRoster.Add(candidate.Label);
                continue;
            }

            if (decisions.GetValueOrDefault(found.IdentityKey) == AttendeeOverrideDecision.Excluded)
                alreadyExcluded++;
            else
                toExclude.Add(found.IdentityKey);
        }

        return (new BulkExcludeOutcome(toExclude.Count, alreadyExcluded, notInRoster, invalidOut, unidentifiable), toExclude);
    }

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailShape();
}
