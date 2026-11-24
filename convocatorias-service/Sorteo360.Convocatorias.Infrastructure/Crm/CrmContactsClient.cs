using System.Net;
using System.Net.Http.Json;

namespace Sorteo360.Convocatorias.Infrastructure.Crm;

/// <summary>
/// Cliente tipado del listado de contactos del CRM. El <c>HttpClient</c> lo arma el chasis
/// (<c>AddServiceClient</c>): trae la URL base configurada, la firma con la identidad propia de
/// este servicio y la envuelve en la tubería de resiliencia. Acá solo viven la ruta y la forma
/// de la respuesta.
/// </summary>
public sealed class CrmContactsClient : ICrmContactsClient
{
    private const string ContactsRoute = "v1/contacts";

    private readonly HttpClient _httpClient;

    public CrmContactsClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<CrmContactPageResponse?> ListContactsAsync(
        IReadOnlyList<string> classifications,
        IReadOnlyList<string> companyCategories,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(classifications);
        ArgumentNullException.ThrowIfNull(companyCategories);

        // Los filtros son repetibles: el mismo nombre de parámetro una vez por valor, que es
        // como el CRM lee un O dentro de una dimensión. Cada valor se escapa, para que uno con
        // un "&" o un acento no parta la petición en dos.
        var parameters = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}",
        };

        parameters.AddRange(classifications.Select(value => $"classification={Uri.EscapeDataString(value)}"));
        parameters.AddRange(companyCategories.Select(value => $"companyCategory={Uri.EscapeDataString(value)}"));

        var route = $"{ContactsRoute}?{string.Join('&', parameters)}";

        using var response = await _httpClient.GetAsync(route, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CrmContactPageResponse>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CrmContactPageResponse?> ListContactsByIdAsync(
        IReadOnlyList<Guid> contactIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contactIds);

        var ids = string.Join('&', contactIds.Select(id => $"id={id}"));
        var route = $"{ContactsRoute}?page={page}&pageSize={pageSize}&{ids}";

        using var response = await _httpClient.GetAsync(route, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CrmContactPageResponse>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CrmContactPageResponse?> FindContactByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        // La dirección se escapa: una con un "+" o un "&" partiría la petición en dos, y un "+"
        // sin escapar llega al otro lado como un espacio.
        var route = $"{ContactsRoute}?email={Uri.EscapeDataString(email)}&page=1&pageSize=1";

        using var response = await _httpClient.GetAsync(route, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CrmContactPageResponse>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CrmCreateContactResponse> CreateContactAsync(
        CrmCreateContactRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(ContactsRoute, request, cancellationToken);

        // El 409 se lee antes de exigir éxito, porque no es un fallo: significa que esa dirección
        // ya la ocupa alguien vivo, y el cuerpo trae su ficha para tomarla y seguir. Exigir éxito
        // primero lo convertiría en una excepción y perdería el dato con el que se resuelve.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content
                .ReadFromJsonAsync<CrmDuplicateEmailProblem>(cancellationToken);

            if (problem?.ExistingContact is null)
            {
                // Un 409 sin ficha no se puede resolver: no hay id que enlazar. Sale como fallo
                // de transporte, que es lo que quien llama ya sabe traducir a 503.
                throw new HttpRequestException(
                    "El CRM rechazó el alta por dirección duplicada y no devolvió el contacto que la ocupa.");
            }

            return new CrmCreateContactResponse(null, problem.ExistingContact);
        }

        response.EnsureSuccessStatusCode();

        var createdId = await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);

        return new CrmCreateContactResponse(createdId, null);
    }

    /// <inheritdoc />
    public async Task<CrmOptOutOutcome> OptOutContactAsync(
        Guid contactId,
        CrmOptOutRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(
            $"{ContactsRoute}/{contactId}/opt-out",
            request,
            cancellationToken);

        // El 404 se lee antes de exigir éxito, porque tampoco es un fallo: significa que el CRM ya
        // no tiene esa ficha —borrada en blando— y entonces no hay nada que empujarle ni nadie a
        // quien pueda escribirle. Exigir éxito primero lo convertiría en una excepción, y como el
        // vaciado recorre todo el servicio y no sella lo que no llegó, esa fila volvería a fallar
        // en cada lanzamiento posterior y los dejaría todos en 503 para siempre.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return CrmOptOutOutcome.ContactGone;

        response.EnsureSuccessStatusCode();

        return CrmOptOutOutcome.Pushed;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CrmMailabilityResponse>?> CheckMailabilityAsync(
        CrmMailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(
            $"{ContactsRoute}/mailability",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<CrmMailabilityResponse>>(cancellationToken);
    }

    /// <inheritdoc />
    public Task<CrmByIdRead<CrmContactListResponse>> GetContactListAsync(
        Guid contactListId,
        CancellationToken cancellationToken) =>
        GetByIdAsync<CrmContactListResponse>($"v1/contact-lists/{contactListId}", cancellationToken);

    /// <summary>
    /// Una lectura por id en la que el 404 se lee antes de exigir éxito: es "no está", una
    /// respuesta, y no una avería del CRM.
    /// </summary>
    private async Task<CrmByIdRead<T>> GetByIdAsync<T>(string route, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await _httpClient.GetAsync(route, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new CrmByIdRead<T>(Found: false, Body: null);

        response.EnsureSuccessStatusCode();

        return new CrmByIdRead<T>(Found: true, await response.Content.ReadFromJsonAsync<T>(cancellationToken));
    }
}
