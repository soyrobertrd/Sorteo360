namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// Los contactos del CRM que este servicio consume: la contactabilidad, que el sorteo usa como lista
/// negra, y la lectura por id, que nombra al contacto patrocinador de un premio. Como los otros clientes del CRM, acá no hay política, solo transporte.
/// <para>
/// Todo fallo de red, de autenticación o de estado sale como <see cref="HttpRequestException"/>,
/// incluido el circuito abierto, que el chasis traduce a esa misma excepción.
/// </para>
/// </summary>
public interface ICrmContactClient
{
    /// <summary>
    /// Pregunta por una tanda de contactos. Un contacto del que el CRM no tiene fila no viene en la
    /// respuesta. Devuelve <c>null</c> si el CRM contestó con el cuerpo vacío.
    /// </summary>
    Task<IReadOnlyList<CrmMailabilityResponse>?> CheckMailabilityAsync(
        CrmMailabilityRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Trae los contactos de estos ids, a lo sumo cien, el tope del CRM. Un id desconocido, borrado o
    /// suprimido no viene, y no es un error. Devuelve <c>null</c> si el CRM contestó con el cuerpo vacío.
    /// </summary>
    Task<CrmContactNamePageResponse?> ListContactsByIdAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken);
}
