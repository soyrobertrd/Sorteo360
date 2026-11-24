namespace Sorteo360.Notifications.Core.DTOs.History;

/// <summary>
/// Una entidad de la plataforma de la que trata un envío, tal como se muestra en el historial.
/// Son ids, no nombres: quien los muestra resuelve el texto contra el servicio dueño.
/// </summary>
public sealed record HistoryAboutDto(string SourceService, string EntityType, string EntityId);
