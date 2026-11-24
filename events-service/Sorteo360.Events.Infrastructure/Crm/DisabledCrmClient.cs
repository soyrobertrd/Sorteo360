namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// Implementación local sin red de los clientes del CRM cuando <c>Services:Crm:BaseUrl</c> no está
/// configurada: devuelve colecciones vacías o nulo para que el servicio arranque y opere de forma
/// autónoma sin depender de <c>crm-service</c>.
/// </summary>
public sealed class DisabledCrmClient : ICrmCompanyClient, ICrmContactClient
{
    public Task<List<CrmCompanyDirectoryEntryResponse>> ListCompaniesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new List<CrmCompanyDirectoryEntryResponse>());

    public Task<CrmCompanyResponse?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult<CrmCompanyResponse?>(null);

    public Task<CrmCompanyContactPageResponse?> GetCompanyContactsAsync(Guid companyId, CancellationToken cancellationToken) =>
        Task.FromResult<CrmCompanyContactPageResponse?>(null);

    public Task<CrmBankAccountResponse?> GetBankAccountAsync(Guid companyId, Guid bankAccountId, CancellationToken cancellationToken) =>
        Task.FromResult<CrmBankAccountResponse?>(null);

    public Task<IReadOnlyList<CrmMailabilityResponse>?> CheckMailabilityAsync(
        CrmMailabilityRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CrmMailabilityResponse>?>(Array.Empty<CrmMailabilityResponse>());

    public Task<CrmContactNamePageResponse?> ListContactsByIdAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<CrmContactNamePageResponse?>(new CrmContactNamePageResponse([]));
}
