namespace Sorteo360.Convocatorias.Core.DTOs;

/// <summary>
/// Editar un borrador. Solo cambian las dos cosas que se congelan al lanzar, y las dos son
/// opcionales: es un <c>PATCH</c>, así que lo que no viene no se toca. Un cuerpo sin ninguna de
/// las dos es una operación sin efecto, no un error.
/// </summary>
/// <param name="Id">La campaña.</param>
/// <param name="TemplateKey">Nueva plantilla, o <c>null</c> para dejar la que tiene.</param>
/// <param name="Language">Nuevo idioma, o <c>null</c> para dejar el que tiene.</param>
/// <param name="Subject">Nuevo asunto de un mensaje directo, o <c>null</c> para dejar el que tiene.</param>
/// <param name="Message">Nuevo texto de un mensaje directo, o <c>null</c> para dejar el que tiene.</param>
/// <param name="UpdatedById">Quién la edita, del token.</param>
public sealed record UpdateCampaignDto(
    Guid Id,
    string? TemplateKey,
    string? Language,
    string? Subject,
    string? Message,
    Guid UpdatedById);
