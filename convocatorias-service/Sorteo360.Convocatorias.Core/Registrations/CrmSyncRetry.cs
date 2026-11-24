namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// Lo que devuelve reintentar a mano la sincronización de una inscripción con el CRM
/// (spec <c>07</c> §6.1): la fila tal como quedó, y lo que dijo el CRM de la ficha.
/// </summary>
/// <param name="Registration">La inscripción, ya sincronizada.</param>
/// <param name="CanBeEmailed">
/// El <c>canBeEmailed</c> de la ficha que devolvió el CRM, o nulo si no hizo falta preguntarle
/// porque la fila ya estaba sincronizada. Se enseña y no se guarda (ver <see cref="CrmSyncOutcome"/>).
/// </param>
public sealed record CrmSyncRetry(Registration Registration, bool? CanBeEmailed);
