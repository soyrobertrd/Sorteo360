using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Los nombres de los patrocinadores de un sorteo, resueltos en el borde y nunca guardados en el
/// agregado (ADR-0013), como el detalle de una cotización resuelve sus empresas. Una sola resolución
/// por respuesta: las empresas desde la copia del directorio, los contactos en vivo y en una sola
/// consulta, y solo si algún premio los tiene.
/// </summary>
public sealed class RaffleSponsorNames
{
    /// <summary>Sin nombres: lo que llevan las respuestas de un premio suelto.</summary>
    public static readonly RaffleSponsorNames None = new(null, null);

    private readonly CompanyLookup? _companies;
    private readonly IReadOnlyDictionary<Guid, string>? _contacts;

    private RaffleSponsorNames(CompanyLookup? companies, IReadOnlyDictionary<Guid, string>? contacts)
    {
        _companies = companies;
        _contacts = contacts;
    }

    public string? CompanyName(Guid companyId) => _companies?.Snapshot(companyId)?.Name;

    public string? ContactName(Guid contactId) => _contacts?.GetValueOrDefault(contactId);

    /// <summary>
    /// Resuelve los patrocinadores de todos los premios del sorteo. Nunca falla: lo que no se pudo
    /// nombrar sale en <c>null</c> y el estado lo informan los directorios a
    /// <c>CrmDirectoryStatusContext</c>.
    /// </summary>
    public static async Task<RaffleSponsorNames> ResolveAsync(
        RaffleWithRounds raffle,
        ICompanyDirectory companyDirectory,
        IContactNameDirectory contactNames,
        CancellationToken cancellationToken)
    {
        var prizes = raffle.Rounds.SelectMany(r => r.Prizes).ToList();
        var companyIds = prizes.Select(p => p.SponsorCompanyId).OfType<Guid>().Distinct().ToList();
        if (companyIds.Count == 0)
            return None;

        var companies = await companyDirectory.ResolveAsync(companyIds, cancellationToken);

        var contactIds = prizes.Select(p => p.SponsorContactId).OfType<Guid>().Distinct().ToList();
        var contacts = contactIds.Count == 0
            ? null
            : (await contactNames.ResolveNamesAsync(contactIds, cancellationToken)).GetValueOrDefault();

        return new RaffleSponsorNames(companies, contacts);
    }
}
