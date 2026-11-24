using Sorteo360.Notifications.Web.NotificationEndpoints;

namespace Sorteo360.Notifications.Web.Validators;

/// <summary>
/// Solo la paginación: es todo lo que el listado de huérfanos recibe, y el techo del tamaño de
/// página aplica igual que en el resto.
/// </summary>
public sealed class OrphanEventsRequestValidator : PagingRequestValidator<OrphanEventsRequest>;
