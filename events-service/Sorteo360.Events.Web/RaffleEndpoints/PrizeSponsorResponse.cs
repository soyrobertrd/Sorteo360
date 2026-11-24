using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// El patrocinador de un premio: los ids que guarda este servicio y los nombres que resolvió el CRM al
/// leer (ADR-0013). Un nombre en <c>null</c> es uno que no se pudo resolver, y la cabecera
/// <c>X-Crm-Directory-Status</c> dice por qué. Las rutas que no devuelven el sorteo entero no
/// resuelven nada: traen solo los ids, con los nombres en <c>null</c> y sin cabecera.
/// </summary>
public record PrizeSponsorResponse
{
    public Guid? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public Guid? ContactId { get; init; }
    public string? ContactName { get; init; }

    /// <summary>El logo de la empresa patrocinadora, o <c>null</c> si no se subió ninguno.</summary>
    public PrizeSponsorLogoResponse? Logo { get; init; }

    /// <summary>El patrocinador del premio, o <c>null</c> si no tiene.</summary>
    public static PrizeSponsorResponse? From(Prize prize, RaffleSponsorNames names, StoredFile? logo)
    {
        if (prize.SponsorCompanyId is { } companyId)
        {
            return new PrizeSponsorResponse
            {
                CompanyId = companyId,
                CompanyName = names.CompanyName(companyId) ?? prize.SponsorName,
                ContactId = prize.SponsorContactId,
                ContactName = prize.SponsorContactId is { } contactId ? names.ContactName(contactId) : null,
                Logo = logo is null ? null : PrizeSponsorLogoResponse.From(logo),
            };
        }

        if (!string.IsNullOrWhiteSpace(prize.SponsorName))
        {
            return new PrizeSponsorResponse
            {
                CompanyId = null,
                CompanyName = prize.SponsorName.Trim(),
                ContactId = null,
                ContactName = null,
                Logo = logo is null ? null : PrizeSponsorLogoResponse.From(logo),
            };
        }

        return null;
    }
}
