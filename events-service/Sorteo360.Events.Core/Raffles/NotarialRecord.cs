namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Una versión del acta notarial de un sorteo: el PDF guardado por el puerto de archivos, quién lo
/// generó y cuándo (spec 03 §1, §4). La versión 1 sale al completar; una reemisión agrega otra y
/// nunca reemplaza la anterior, así que ninguna fila se edita ni se borra.
/// </summary>
public sealed record NotarialRecord(
    Guid Id,
    Guid RaffleId,
    int Version,
    Guid StoredFileId,
    DateTimeOffset GeneratedAt,
    Guid GeneratedById)
{
    /// <summary>La versión que sale al completar; una reemisión escribe la siguiente.</summary>
    public const int FirstVersion = 1;
}
