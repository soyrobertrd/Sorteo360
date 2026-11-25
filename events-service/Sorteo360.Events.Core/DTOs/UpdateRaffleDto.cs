using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Core.DTOs;

/// <summary>
/// La representación editable completa de un sorteo (ADR-0009): los tres ajustes de pantalla, la
/// confirmación de presencia, las dos reglas, las autoridades y las instrucciones de retiro. Lo que
/// se omitió llega en blanco y se borra. Las rondas y los premios no viajan acá: si viajaran,
/// omitirlos los borraría. DTO de entrada: validado en el borde por UpdateRaffleRequestValidator.
/// </summary>
public record UpdateRaffleDto(
    Guid EventId,
    string VisualStyle,
    bool ShowPrizeOnScreen,
    int AnimationSeconds,
    bool RequiresPresenceConfirmation,
    bool OnePrizePerPerson,
    bool OnePrizePerCompany,
    RaffleAuthorities Authorities,
    string? PickupInstructions,
    Guid UpdatedById,
    bool? RequiresCheckIn = null,
    string? BoardTitle = null,
    string? BoardSubtitle = null,
    string? BoardIdentifier = null,
    int? BoardBatchSize = null,
    Guid? LinkedEventId = null);
