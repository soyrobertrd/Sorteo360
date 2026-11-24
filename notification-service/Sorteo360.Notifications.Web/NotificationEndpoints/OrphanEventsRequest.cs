using Sorteo360.Notifications.Web.GlobalEndpoints;

namespace Sorteo360.Notifications.Web.NotificationEndpoints;

/// <summary>
/// Request del listado de eventos huérfanos: solo paginación. No lleva filtros porque un evento
/// sin correlacionar no tiene por dónde filtrarse; lo que se quiere de esta lista es verla entera.
/// </summary>
public record OrphanEventsRequest : PagingRequest;
