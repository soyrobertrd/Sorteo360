namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Estados de un sorteo: draft → in_progress al iniciarlo, y de ahí a completed al cerrarlo.
/// Solo avanza, y solo por una acción explícita del operador; nada en sus premios ni en sus
/// sorteos lo mueve. No hay estado <c>configured</c>: si está listo para iniciar se calcula al
/// leer (spec 00 §2).
/// </summary>
public static class RaffleState
{
    public const string Draft      = "draft";
    public const string InProgress = "in_progress";
    public const string Completed  = "completed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Draft, InProgress, Completed
    };
}
