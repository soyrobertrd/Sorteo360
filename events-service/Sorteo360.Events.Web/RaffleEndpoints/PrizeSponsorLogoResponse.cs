using Sorteo360.Events.Core.Files;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// El logo del patrocinador de un premio. Los bytes se piden a <c>GET /v1/files/{fileId}/content</c>.
/// </summary>
public sealed record PrizeSponsorLogoResponse
{
    public Guid FileId { get; init; }
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// El almacenamiento ya no tiene los bytes. El logo se sigue devolviendo: el nombre es lo que dice
    /// qué hay que restaurar, y la pantalla muestra un hueco en lugar de romperse.
    /// </summary>
    public bool Missing { get; init; }

    public static PrizeSponsorLogoResponse From(StoredFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new PrizeSponsorLogoResponse
        {
            FileId = file.Id,
            FileName = file.FileName,
            Missing = file.MissingSince is not null,
        };
    }
}
