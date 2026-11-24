using CSharpFunctionalExtensions;
using Sorteo360.Events.Core.FeatureSwitches;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Database;

/// <summary>
/// Repositorio de interruptores de funciones en <c>events-service</c>.
/// </summary>
public interface IFeatureSwitchRepository
{
    Task<Result<IReadOnlyList<FeatureSwitchSetting>, BaseError>> ListAsync(CancellationToken cancellationToken);

    Task<Result<FeatureSwitchSetting, BaseError>> UpsertAsync(
        FeatureSwitchSetting setting,
        CancellationToken cancellationToken);
}

