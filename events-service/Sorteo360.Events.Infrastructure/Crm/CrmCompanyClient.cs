using System.Net;
using System.Net.Http.Json;

namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// Cliente tipado de las empresas del CRM. El <c>HttpClient</c> lo arma el chasis
/// (<c>AddServiceClient</c>): trae la URL base configurada, la firma con la identidad propia de
/// este servicio y la envuelve en la tubería de resiliencia. Acá solo viven las rutas y la forma de
/// las respuestas.
/// </summary>
public sealed class CrmCompanyClient : ICrmCompanyClient
{
    private const string CompaniesRoute = "v1/companies";
    private const string DirectoryRoute = "v1/companies/directory";

    /// <summary>El tope que admite la ruta de contactos del CRM.</summary>
    private const int ContactPageSize = 100;

    private readonly HttpClient _httpClient;

    public CrmCompanyClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<List<CrmCompanyDirectoryEntryResponse>> ListCompaniesAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(DirectoryRoute, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Un cuerpo vacío sería una respuesta con éxito y sin directorio: se trata como lista vacía
        // en lugar de reventar, y el resultado es el mismo que no haber podido resolver ningún nombre.
        return await response.Content.ReadFromJsonAsync<List<CrmCompanyDirectoryEntryResponse>>(cancellationToken)
            ?? [];
    }

    /// <inheritdoc />
    public async Task<CrmCompanyResponse?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"{CompaniesRoute}/{companyId}", cancellationToken);

        // El 404 es una respuesta del CRM, no una avería: ese id nunca existió. Se distingue del
        // resto para que quien valida pueda rechazarlo sin confundirlo con el CRM caído.
        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CrmCompanyResponse>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CrmCompanyContactPageResponse?> GetCompanyContactsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        // Se pide la página máxima que admite el CRM y no se sigue paginando: una empresa con más
        // de cien contactos registrados no es el caso que esta pantalla resuelve, y encadenar
        // páginas metería varias idas al CRM dentro de una lectura que ya depende de que conteste.
        using var response = await _httpClient.GetAsync(
            $"{CompaniesRoute}/{companyId}/contacts?page=1&pageSize={ContactPageSize}",
            cancellationToken);

        // Igual que la lectura por id: el 404 es el CRM diciendo que esa empresa no existe, no una
        // avería, y quien llama lo distingue de que no conteste.
        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CrmCompanyContactPageResponse>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CrmBankAccountResponse?> GetBankAccountAsync(
        Guid companyId,
        Guid bankAccountId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"{CompaniesRoute}/{companyId}/bank-accounts/{bankAccountId}",
            cancellationToken);

        // El 404 es el CRM diciendo que esa empresa no tiene esa cuenta, no una avería.
        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CrmBankAccountResponse>(cancellationToken);
    }
}
