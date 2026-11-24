namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.notarial_record</c> (una versión del acta de un
/// sorteo, con el archivo que la guarda).
/// </summary>
internal sealed class NotarialRecordData
{
    public Guid Id { get; set; }
    public Guid RaffleId { get; set; }
    public int Version { get; set; }
    public Guid StoredFileId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public Guid GeneratedById { get; set; }
}
