using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>Los destinatarios de campaña leídos por contacto del CRM, no por campaña.</summary>
public interface IContactCampaignRecipientRepository
{
    /// <summary>
    /// Una fila por contacto y campaña lanzada que lo tuvo en su lista, de los contactos pedidos y de
    /// ningún otro, de la más reciente a la más antigua. Un borrador no cuenta: a nadie se le escribió.
    /// Un contacto sin campañas no aparece, porque este servicio no sabe qué contactos existen.
    /// </summary>
    Task<Result<IReadOnlyList<ContactCampaignRecipient>, BaseError>> ListByContactsAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken);
}
