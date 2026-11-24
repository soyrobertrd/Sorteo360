namespace Sorteo360.Convocatorias.Infrastructure.Crm;

/// <summary>
/// Implementación local sin red de <see cref="ICrmContactsClient"/> cuando
/// <c>Services:Crm:BaseUrl</c> no está configurada: permite que convocatorias-service arranque sin
/// depender de <c>crm-service</c>.
/// </summary>
public sealed class DisabledCrmContactsClient : ICrmContactsClient
{
    public Task<CrmContactPageResponse?> ListContactsAsync(
        IReadOnlyList<string> classifications,
        IReadOnlyList<string> companyCategories,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        Task.FromResult<CrmContactPageResponse?>(new CrmContactPageResponse([], 0, page, pageSize));

    public Task<CrmContactPageResponse?> ListContactsByIdAsync(
        IReadOnlyList<Guid> contactIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        Task.FromResult<CrmContactPageResponse?>(new CrmContactPageResponse([], 0, page, pageSize));

    public Task<CrmContactPageResponse?> FindContactByEmailAsync(
        string email,
        CancellationToken cancellationToken) =>
        Task.FromResult<CrmContactPageResponse?>(new CrmContactPageResponse([], 0, 1, 1));

    public Task<CrmCreateContactResponse> CreateContactAsync(
        CrmCreateContactRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CrmCreateContactResponse(Guid.NewGuid(), null));

    public Task<CrmOptOutOutcome> OptOutContactAsync(
        Guid contactId,
        CrmOptOutRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(CrmOptOutOutcome.ContactGone);

    public Task<IReadOnlyList<CrmMailabilityResponse>?> CheckMailabilityAsync(
        CrmMailabilityRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CrmMailabilityResponse>?>(Array.Empty<CrmMailabilityResponse>());

    public Task<CrmByIdRead<CrmContactListResponse>> GetContactListAsync(
        Guid contactListId,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CrmByIdRead<CrmContactListResponse>(false, null));
}

