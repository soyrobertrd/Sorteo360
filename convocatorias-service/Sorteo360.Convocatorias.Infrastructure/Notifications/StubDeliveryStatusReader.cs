using Sorteo360.Convocatorias.Core.Notifications;

namespace Sorteo360.Convocatorias.Infrastructure.Notifications;

/// <summary>
/// La entrega con el transporte de stub: no hay notification-service al que preguntarle, y el stub
/// no devuelve ids de mensaje, así que no se sabe nada y todo lo entregado se ve como "enviado".
/// </summary>
public sealed class StubDeliveryStatusReader : IDeliveryStatusReader
{
    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, string>> ReadAsync(
        IReadOnlyCollection<Guid> notificationIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
}
