namespace Sorteo360.Events.Web.FileEndpoints;

/// <summary>
/// Ficha de un archivo: lo que hace falta para reconocerlo en una pantalla. La clave de
/// almacenamiento y el proveedor no salen nunca — no significan nada para quien lee, y son
/// justamente lo que puede cambiar.
/// </summary>
public sealed record FileResponse
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// El almacenamiento ya no tiene los bytes. La ficha se sigue devolviendo: el nombre y la
    /// fecha son lo que dice qué hay que restaurar.
    /// </summary>
    public bool Missing { get; init; }
}
