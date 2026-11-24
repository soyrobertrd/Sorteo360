namespace Sorteo360.Convocatorias.Core.DTOs;

/// <summary>Crear un borrador, para un evento o como mensaje directo sin evento.</summary>
/// <param name="EventId">El evento que se convoca, o <c>null</c> en un mensaje directo.</param>
/// <param name="TemplateKey">Qué correo sale.</param>
/// <param name="Language">En qué idioma.</param>
/// <param name="Subject">El asunto de un mensaje directo; <c>null</c> en una campaña de evento.</param>
/// <param name="Message">El texto de un mensaje directo; <c>null</c> en una campaña de evento.</param>
/// <param name="CreatedById">Quién lo crea, del token.</param>
public sealed record CreateCampaignDto(
    Guid? EventId,
    string TemplateKey,
    string Language,
    string? Subject,
    string? Message,
    Guid CreatedById);
