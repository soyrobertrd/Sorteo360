namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Lo que hizo una exclusión en lote: cuántas exclusiones escribió, cuántas ya estaban, y a quién no
/// pudo excluir y por qué. Lo que no se encontró se informa, no falla.
/// </summary>
/// <param name="Excluded">Exclusiones nuevas, o inclusiones que pasaron a exclusión.</param>
/// <param name="AlreadyExcluded">Ya estaban excluidas; no se tocan.</param>
/// <param name="NotInRoster">No están en el padrón o en la puerta del evento destino.</param>
/// <param name="Invalid">No parecen correo y ningún participante tiene ese ID.</param>
/// <param name="Unidentifiable">Ganadores de la fuente sin correo ni ID, por su nombre.</param>
public sealed record BulkExcludeOutcome(
    int Excluded,
    int AlreadyExcluded,
    IReadOnlyList<string> NotInRoster,
    IReadOnlyList<string> Invalid,
    IReadOnlyList<string> Unidentifiable);

/// <summary>Una persona del padrón de un evento, con lo que la reconoce: su clave, su correo y su ID.</summary>
public sealed record RosterPerson(string IdentityKey, string? Email, string? EmployeeCode);
