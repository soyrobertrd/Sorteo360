using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Database;

/// <summary>Los check-ins leídos por contacto del CRM, no por evento.</summary>
public interface IContactCheckInRepository
{
    /// <summary>
    /// Una fila por contacto y evento al que asistió, de los contactos pedidos y de ningún otro, de
    /// la más reciente a la más antigua. Un contacto sin check-ins no aparece: no es un error,
    /// porque este servicio no sabe qué contactos existen.
    /// </summary>
    Task<Result<IReadOnlyList<ContactCheckIn>, BaseError>> ListByContactsAsync(
        IReadOnlyCollection<Guid> contactIds,
        CancellationToken cancellationToken);
}
