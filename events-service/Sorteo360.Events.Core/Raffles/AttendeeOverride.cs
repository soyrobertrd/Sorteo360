namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// La decisión manual sobre un asistente: alguien lo metió al bombo o lo sacó (spec 01 §4.2). Es lo
/// único del sorteo que se guarda antes de arrancar, y se aplica en cada cálculo, así que la gente
/// que sigue llegando no cambia nada de lo que un operador decidió.
/// <para>
/// La identidad es la <see cref="PersonIdentity"/> del asistente, igual que la del asistente. No se borra al arrancar ni
/// cuando pierde: una inclusión escrita antes de que el CRM vetara a la persona sigue ahí, y el
/// cálculo dice qué la venció.
/// </para>
/// </summary>
public sealed record AttendeeOverride(
    string IdentityKey,
    string Decision,
    string? Note,
    Guid DecidedById,
    DateTimeOffset DecidedAt);

/// <summary>Los dos sentidos de una decisión manual.</summary>
public static class AttendeeOverrideDecision
{
    /// <summary>Entra al bombo aunque un motivo superable lo dejara fuera.</summary>
    public const string Included = "included";

    /// <summary>Queda fuera del bombo aunque calificara.</summary>
    public const string Excluded = "excluded";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Included, Excluded
    };
}
