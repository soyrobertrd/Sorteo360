namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Las cifras de todos los sorteos juntos. <c>Active</c> son los que están en curso; <c>Winners</c>,
/// los sorteos de premio vivos; <c>Redeemed</c>, los vivos con una entrega en pie; y
/// <c>QualifiedParticipants</c> suma los bombos congelados, así que un borrador no aporta nada: su
/// puerta todavía cambia.
/// </summary>
public sealed record RaffleTotals(
    int Raffles,
    int Active,
    int Prizes,
    int Winners,
    int Redeemed,
    int QualifiedParticipants);
