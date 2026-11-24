namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Los nombres de quienes operaron el sorteo, leídos del realm por id para escribirlos en el acta
/// (spec 03 §4.4 paso 4). El acta congela el texto, así que se pregunta una vez y en vivo.
/// </summary>
public interface IRaffleOperatorDirectory
{
    /// <summary>
    /// Un nombre por cada id pedido. Nulo es que el realm no pudo decirlo, por la razón que sea: el
    /// acta lo escribe como usuario no disponible, y completar nunca falla por eso.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string?>> ResolveNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}
