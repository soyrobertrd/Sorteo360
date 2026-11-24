namespace Sorteo360.Events.Core.DTOs;

public sealed record ImportPrizeItemDto(
    int RoundNumber,
    string Name,
    string? Value,
    int Quantity,
    string? SponsorName = null);

public sealed record ImportPrizesDto(
    Guid EventId,
    IReadOnlyList<ImportPrizeItemDto> Prizes,
    Guid CreatedById);

public sealed record ImportPrizesResult(
    int Created,
    int RoundsCreated);

