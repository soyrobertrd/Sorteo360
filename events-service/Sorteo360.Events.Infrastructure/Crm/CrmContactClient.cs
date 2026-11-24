using System.Net.Http.Json;

namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// Cliente tipado de los contactos del CRM. El <c>HttpClient</c> lo arma el chasis
/// (<c>AddServiceClient</c>); acá solo viven la ruta y la forma de la respuesta. La ruta no exige
/// rol, así que basta la identidad propia de este servicio.
/// </summary>
public sealed class CrmContactClient : ICrmContactClient
{
    private const string MailabilityRoute = "v1/contacts/mailability";
    private const string ContactsRoute = "v1/contacts";

    /// <summary>La página máxima del CRM, que coincide con su tope de ids.</summary>
    private const int ContactPageSize = 100;

    private readonly HttpClient _httpClient;

    public CrmContactClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CrmMailabilityResponse>?> CheckMailabilityAsync(
        CrmMailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(MailabilityRoute, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<CrmMailabilityResponse>>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CrmContactNamePageResponse?> ListContactsByIdAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contactIds);

        var ids = string.Join('&', contactIds.Select(id => $"id={id}"));
        using var response = await _httpClient.GetAsync(
            $"{ContactsRoute}?page=1&pageSize={ContactPageSize}&{ids}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CrmContactNamePageResponse>(cancellationToken);
    }
}
