namespace Sorteo360.Convocatorias.Infrastructure.Events;

/// <summary>
/// Las lecturas de eventos y el alta de un ServiceSupplier que este servicio le pide a
/// events-service. Es el único sitio de convocatorias donde aparece una ruta de eventos: el chasis
/// sabe autenticarse y sobrevivir a la red, pero no conoce ninguna ruta ni ningún DTO, y el dominio
/// no sabe siquiera que hay una red.
/// <para>
/// Todo fallo de red, de autenticación o de estado sale como <see cref="HttpRequestException"/>,
/// que es lo que garantiza el chasis. Quien llama lo traduce a un <c>Result</c>.
/// </para>
/// </summary>
public interface IEventsClient
{
    /// <summary>
    /// Un evento por id. <c>null</c> cuando events-service contesta 404, que no es una avería:
    /// es un id que no conoce.
    /// </summary>
    Task<EventItemResponse?> GetEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Una página del listado filtrado por visibilidad y por evento no terminado.</summary>
    /// <param name="visibility">La visibilidad pedida, tal como la nombra events-service.</param>
    /// <param name="page">Página pedida, 1-based.</param>
    /// <param name="pageSize">Tamaño de página pedido; events-service lo topa en 100.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<EventPageResponse?> ListActiveEventsAsync(
        string visibility,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Una página de <c>GET /v1/events?status=</c>, de cualquier visibilidad. Nulo si el cuerpo
    /// llega vacío; una avería de transporte sale como excepción, igual que en el listado activo.
    /// </summary>
    Task<EventPageResponse?> ListEventsByStatusAsync(
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Crea un ServiceSupplier, <c>POST /v1/service-suppliers</c>, y devuelve su id. <b>El 409
    /// <c>SUPPLIER_COMPANY_ALREADY_REGISTERED</c> no es un fallo de transporte</b>: la empresa ya es
    /// proveedor en esa categoría, y llega como <c>null</c>. Cualquier otra respuesta no exitosa llega
    /// como excepción.
    /// </summary>
    /// <param name="request">El cuerpo del alta, sin catálogo de servicios.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<Guid?> CreateServiceSupplierAsync(EventsCreateServiceSupplierRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Las fichas vivas de una empresa, <c>GET /v1/service-suppliers?companyId=</c>. Nulo si el cuerpo
    /// llega vacío; cualquier respuesta no exitosa llega como excepción.
    /// </summary>
    /// <param name="companyId">La empresa del CRM.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<IReadOnlyList<EventsServiceSupplierListItem>?> ListServiceSuppliersByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken);
}
