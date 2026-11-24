using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.stored_file</c>: lo que el almacenamiento sabe
/// de un archivo. No tiene <c>deleted_at</c> a propósito — lo que se da de baja es el enlace, no
/// los bytes.
/// </summary>
internal sealed class StoredFileData
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; } = Account.DefaultBanreservasAccountId;
    public string Provider { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public DateTimeOffset? MissingSince { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedById { get; set; }
}
