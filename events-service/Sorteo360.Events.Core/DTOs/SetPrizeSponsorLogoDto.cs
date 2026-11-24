namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// La subida del logo del patrocinador de un premio: los bytes y lo que hace falta para identificar el
/// archivo. El contenido viaja como <see cref="Stream"/>, como el de cualquier otra subida.
/// </summary>
public record SetPrizeSponsorLogoDto(
    Guid EventId,
    Guid PrizeId,
    Stream Content,
    string FileName,
    string ContentType,
    Guid UploadedById);
