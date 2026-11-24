namespace Sorteo360.Convocatorias.Core.DTOs;

/// <summary>
/// Lanzar. Lleva la cuenta que quien lanza cree estar lanzando, y es obligatoria: quitar el
/// paso de someter quitó el momento en que dos personas editando una campaña se descubrían
/// (spec <c>03</c> §5.1).
/// </summary>
/// <param name="Id">La campaña.</param>
/// <param name="ExpectedRecipientCount">Los destinatarios que quien lanza tenía en pantalla.</param>
/// <param name="LaunchedById">Quién apretó, del token.</param>
/// <param name="LauncherOptCode">
/// El <c>opt_code</c> de quien apretó, o nulo si no trae (administración central). Decide desde
/// qué dirección sale la campaña (PGTI-168).
/// </param>
public sealed record LaunchCampaignDto(
    Guid Id,
    int ExpectedRecipientCount,
    Guid LaunchedById,
    string? LauncherOptCode = null);
