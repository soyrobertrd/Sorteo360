namespace Sorteo360.Notifications.Core.DTOs;

/// <summary>
/// Un envío tal como lo pide quien llama: el correo entero, los datos con que se sustituye, las
/// personas, y las entidades de las que trata.
/// </summary>
/// <param name="Type">Etiqueta de historial, en minúsculas y con puntos.</param>
/// <param name="Language">Uno de los seis idiomas permitidos. Sin valor por omisión.</param>
/// <param name="FromAddress">Quién firma el correo. Este servicio no averigua qué oficina manda.</param>
/// <param name="FromName">Nombre visible del remitente.</param>
/// <param name="Subject">Asunto, con marcadores.</param>
/// <param name="Body">Cuerpo HTML completo, con marcadores.</param>
/// <param name="SharedData">Valores que aplican a todas las personas.</param>
/// <param name="Recipients">Las personas, de 1 a 1000.</param>
/// <param name="About">Las entidades de las que trata el envío.</param>
/// <param name="IdempotencyKey">Clave acuñada por quien llama, derivada de una clave natural.</param>
/// <param name="TraceId">Traza de la petición que lo aceptó, para poder seguirla entre servicios.</param>
public record SendNotificationDto(
    string Type,
    string Language,
    string FromAddress,
    string? FromName,
    string Subject,
    string Body,
    IReadOnlyDictionary<string, string> SharedData,
    IReadOnlyList<NotificationRecipientDto> Recipients,
    IReadOnlyList<NotificationAboutDto> About,
    string IdempotencyKey,
    string? TraceId);

/// <summary>
/// Una persona del envío. El servicio no puede distinguir una persona de dentro del MITUR de una
/// de fuera, y no debe: las dos llegan igual, y toda diferencia entre ellas vive en quien llama.
/// </summary>
/// <param name="Email">Dirección de la persona.</param>
/// <param name="Name">Nombre al que va dirigido el correo, ya recortado; null si no vino o vino vacío.</param>
/// <param name="Data">Valores propios de esta persona, si tiene.</param>
public record NotificationRecipientDto(
    string Email,
    string? Name,
    IReadOnlyDictionary<string, string> Data);

/// <summary>Una entidad de la plataforma sobre la que va el envío.</summary>
/// <param name="SourceService">Servicio dueño de la entidad.</param>
/// <param name="EntityType">Tipo de entidad.</param>
/// <param name="EntityId">Id de la entidad en su servicio.</param>
public record NotificationAboutDto(
    string SourceService,
    string EntityType,
    string EntityId);
