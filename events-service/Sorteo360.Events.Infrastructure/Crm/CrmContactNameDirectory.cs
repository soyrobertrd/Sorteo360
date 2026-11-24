using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.CrmDirectory;

namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// Los nombres de contactos, siempre en vivo contra el CRM (<c>GET v1/contacts?id=</c>). Sin copia en
/// caché, igual que <see cref="CrmCompanyContactDirectory"/>, y con su misma degradación: o se
/// resuelve todo, o la respuesta entera va degradada.
/// </summary>
public sealed class CrmContactNameDirectory : IContactNameDirectory
{
    /// <summary>Cuántos ids admite el CRM en una sola consulta, que es también su página máxima.</summary>
    private const int BatchSize = 100;

    private readonly ICrmContactClient _client;
    private readonly CrmDirectoryStatusContext _statusContext;
    private readonly ILogger<CrmContactNameDirectory> _logger;

    public CrmContactNameDirectory(
        ICrmContactClient client,
        CrmDirectoryStatusContext statusContext,
        ILogger<CrmContactNameDirectory> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(statusContext);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _statusContext = statusContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Maybe<IReadOnlyDictionary<Guid, string>>> ResolveNamesAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contactIds);

        var names = new Dictionary<Guid, string>();

        try
        {
            // Un sorteo con más de cien contactos patrocinadores no existe, así que en la práctica es
            // una sola llamada; las tandas solo evitan que el día que exista se convierta en un 400.
            foreach (var batch in contactIds.Distinct().Chunk(BatchSize))
            {
                var page = await _client.ListContactsByIdAsync(batch, cancellationToken);
                foreach (var contact in page?.Items ?? [])
                    names[contact.Id] = contact.Name;
            }

            // Un acierto no informa nada, como CrmCompanyContactDirectory: el contexto se queda con el
            // peor estado, y quien resolvió las empresas ya puso el suyo.
            return Maybe<IReadOnlyDictionary<Guid, string>>.From(names);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudieron consultar {Count} contactos en el CRM; la respuesta va sin sus nombres.",
                contactIds.Count);

            _statusContext.Report(CrmDirectoryStatus.Stale);

            return Maybe<IReadOnlyDictionary<Guid, string>>.None;
        }
    }
}
