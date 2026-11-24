namespace Sorteo360.Convocatorias.Infrastructure.Crm;

/// <summary>
/// Las rutas del CRM que usa este servicio: contactos, oficinas de OPT, empresas y fuentes de
/// origen. Es el único sitio de convocatorias donde aparece una ruta del CRM: el chasis sabe
/// autenticarse y sobrevivir a la red, pero no conoce ninguna ruta ni ningún DTO, y el dominio no
/// sabe siquiera que hay una red.
/// <para>
/// Todo fallo de red, de autenticación o de estado sale como <see cref="HttpRequestException"/>,
/// que es lo que garantiza el chasis. Quien llama lo traduce a un <c>Result</c>; acá no hay
/// política de degradación, solo transporte.
/// </para>
/// </summary>
public interface ICrmContactsClient
{
    /// <summary>Una página del listado filtrado. El CRM topa <paramref name="pageSize"/> en 100.</summary>
    /// <param name="classifications">Clasificaciones de contacto; O entre ellas.</param>
    /// <param name="companyCategories">Categorías de empresa; O entre ellas, Y con las anteriores.</param>
    /// <param name="page">Página pedida, 1-based.</param>
    /// <param name="pageSize">Tamaño de página pedido.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<CrmContactPageResponse?> ListContactsAsync(
        IReadOnlyList<string> classifications,
        IReadOnlyList<string> companyCategories,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una página de los contactos con esos ids, repetidos como <c>id=</c>. El CRM acepta hasta
    /// 100 por llamada y no devuelve los que no existen o se borraron.
    /// </summary>
    /// <param name="contactIds">Los contactos pedidos, a lo sumo 100.</param>
    /// <param name="page">Página pedida, 1-based.</param>
    /// <param name="pageSize">Tamaño de página pedido.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<CrmContactPageResponse?> ListContactsByIdAsync(
        IReadOnlyList<Guid> contactIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una página filtrada por dirección exacta. Es un filtro aparte de <c>search</c>, que es un
    /// <c>ILIKE '%term%'</c> sobre nombre y correo: casar por ahí ataría <c>ana@acme.com</c> a la
    /// ficha de <c>mariana@acme.com</c>.
    /// </summary>
    /// <param name="email">La dirección, cruda salvo el recorte: normaliza el CRM.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<CrmContactPageResponse?> FindContactByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Crea un contacto suelto. <b>Un 409 no es un fallo de transporte</b> y no sale como
    /// excepción: llega dentro de la respuesta, con la ficha que ya ocupaba la dirección.
    /// </summary>
    /// <param name="request">El cuerpo del alta.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<CrmCreateContactResponse> CreateContactAsync(
        CrmCreateContactRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Empuja una baja. El CRM la trata como idempotente: repetirla contesta 200, no 409, así
    /// que reintentar un vaciado cortado a medias no necesita saber por dónde iba.
    /// <para>
    /// <b>Un 404 no es un fallo de transporte</b> y no sale como excepción: llega dentro de la
    /// respuesta, porque el CRM que ya no tiene esa ficha tampoco le va a escribir.
    /// </para>
    /// </summary>
    /// <param name="contactId">El contacto que se dio de baja.</param>
    /// <param name="request">El cuerpo de la baja.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<CrmOptOutOutcome> OptOutContactAsync(
        Guid contactId,
        CrmOptOutRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una tanda de veredictos de contactabilidad. <b>Una llamada, como mucho 1.000 ids</b>: el
    /// tope es del CRM y partir la lista es de quien llama, no de acá.
    /// </summary>
    /// <param name="request">Los ids por los que se pregunta.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<IReadOnlyList<CrmMailabilityResponse>?> CheckMailabilityAsync(
        CrmMailabilityRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una lista de contactos por id, <c>GET /v1/contact-lists/{id}</c>. Con
    /// <c>crm:service-read</c> el CRM la devuelve sea de la OPT que sea. Un 404 llega dentro de
    /// la respuesta y no como excepción.
    /// </summary>
    /// <param name="contactListId">La lista.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<CrmByIdRead<CrmContactListResponse>> GetContactListAsync(
        Guid contactListId,
        CancellationToken cancellationToken);
}
