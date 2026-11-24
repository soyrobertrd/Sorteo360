using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.Branding;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Database;

/// <summary>
/// Contrato de persistencia para la marca global de Sorteo360 y la marca opcional por evento.
/// </summary>
public interface IBrandingRepository
{
    Task<Result<GlobalBrandingSettings, BaseError>> GetGlobalAsync(CancellationToken cancellationToken);

    Task<Result<GlobalBrandingSettings, BaseError>> UpsertGlobalAsync(
        GlobalBrandingSettings settings,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyDictionary<Guid, EventBrandingSettings>, BaseError>> ListEventBrandingsAsync(
        CancellationToken cancellationToken);

    Task<Result<EventBrandingSettings?, BaseError>> GetEventBrandingAsync(
        Guid eventId,
        CancellationToken cancellationToken);

    Task<UnitResult<BaseError>> UpsertEventBrandingAsync(
        EventBrandingSettings settings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resuelve la marca pública de un evento específico para pantallas anónimas (portal, escáner)
    /// a partir de la cuenta dueña del evento (S5).
    /// </summary>
    Task<Result<(GlobalBrandingSettings Global, EventBrandingSettings? EventBranding), BaseError>> GetPublicForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken);
}

