using System.Net;
using System.Net.Http.Json;

namespace Sorteo360.Convocatorias.Infrastructure.Events;

/// <summary>
/// Cliente tipado de las rutas de events-service que usa este servicio. El
/// <c>HttpClient</c> lo arma el chasis (<c>AddServiceClient</c>): trae la URL base configurada
/// en <c>Services__Events__BaseUrl</c>, la firma con la identidad propia de este servicio y la
/// envuelve en la tubería de resiliencia. Acá solo viven la ruta y la forma de la respuesta.
/// </summary>
public sealed class EventsClient : IEventsClient
{
    private const string EventsRoute = "v1/events";

    private readonly HttpClient _httpClient;

    public EventsClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<EventItemResponse?> GetEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"{EventsRoute}/{eventId}", cancellationToken);

        // El 404 se lee antes de exigir éxito: no es un fallo de transporte sino la respuesta, y
        // exigir éxito primero lo convertiría en excepción y por tanto en un 503 inventado.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<EventItemResponse>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EventPageResponse?> ListActiveEventsAsync(
        string visibility,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(visibility);

        // 'active' y 'status' son excluyentes del otro lado: se manda el atajo, que es el que
        // dice "todavía no terminó" sin enumerar estados que allá pueden crecer.
        var route = $"{EventsRoute}?visibility={Uri.EscapeDataString(visibility)}" +
                    $"&active=true&page={page}&pageSize={pageSize}";

        using var response = await _httpClient.GetAsync(route, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<EventPageResponse>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EventPageResponse?> ListEventsByStatusAsync(
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        var route = $"{EventsRoute}?status={Uri.EscapeDataString(status)}&page={page}&pageSize={pageSize}";

        using var response = await _httpClient.GetAsync(route, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<EventPageResponse>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid?> CreateServiceSupplierAsync(
        EventsCreateServiceSupplierRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync("v1/service-suppliers", request, cancellationToken);

        // Una empresa que ya es proveedor en esa categoría es una respuesta, no una avería: un
        // reenvío la encuentra ahí. Un 409 con otro código no se sabe leer y sale como avería.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<EventsProblem>(cancellationToken);

            if (problem?.Code == EventsProblem.CompanyAlreadyRegisteredCode)
                return null;

            throw new HttpRequestException(
                $"events-service rechazó el alta del ServiceSupplier con un 409 que no es de categoría repetida: {problem?.Code}.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventsServiceSupplierListItem>?> ListServiceSuppliersByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"v1/service-suppliers?companyId={companyId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<EventsServiceSupplierListItem>>(cancellationToken);
    }
}
