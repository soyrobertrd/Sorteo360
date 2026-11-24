using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Raffles;

/// <summary>
/// Lo que el CRM dice de cada contacto: si MITUR lo puede invitar y, si no, por qué (spec 01 §3,
/// §3.1). Es la respuesta de invitación del CRM, no la de correo: darse de baja de las campañas no
/// saca a nadie del bombo. Nada en esta firma dice que la respuesta llegue por la red.
/// </summary>
public interface IContactMailability
{
    /// <summary>
    /// Por qué no se puede invitar a cada contacto, por su id: <c>null</c> cuando se le puede
    /// invitar; si no, el <c>inviteSuppressionReason</c> del CRM, nunca <c>null</c>.
    /// <para>
    /// Todo o nada: si una sola tanda falla, falla la consulta entera con <c>CRM_UNAVAILABLE</c>, y
    /// nunca se devuelve una respuesta parcial, porque los que quedaran sin contestar se leerían
    /// como ausentes.
    /// </para>
    /// <para>
    /// Un id que no viene en la respuesta es un contacto del que el CRM no tiene fila —uno borrado,
    /// por ejemplo—, y no uno al que se le puede escribir.
    /// </para>
    /// </summary>
    Task<Result<IReadOnlyDictionary<Guid, string?>, BaseError>> CheckAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken);
}
