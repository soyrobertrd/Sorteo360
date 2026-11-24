using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// Alta de una imagen en un evento: los bytes, qué papel cumple, y lo que hace falta para
/// identificar el archivo. El contenido viaja como <see cref="Stream"/> para que un archivo
/// grande no pase entero por la memoria del servicio.
/// </summary>
public record AddEventImageDto(
    Guid EventId,
    Stream Content,
    string FileName,
    string ContentType,
    EventImageKind Kind,
    Guid CreatedById);
