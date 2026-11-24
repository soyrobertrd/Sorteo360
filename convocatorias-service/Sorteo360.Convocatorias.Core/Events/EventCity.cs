namespace Sorteo360.Convocatorias.Core.Events;

/// <summary>
/// Una ciudad del recorrido del evento. De acá sale <c>{{lugar}}</c> de la convocatoria: no hay
/// sede en el evento —una sede es un ServiceSupplier aprobado en la cotización de local, que
/// suele no estar decidido cuando sale el save-the-date— así que el lugar es la ciudad, y en un
/// roadshow de varias es la lista (spec <c>01</c> §4.1).
/// </summary>
/// <param name="Name">Nombre de la ciudad.</param>
/// <param name="Country">País.</param>
/// <param name="Date">El día que le toca, si ya se fijó.</param>
/// <param name="DisplayOrder">Su lugar en el recorrido.</param>
public sealed record EventCity(string Name, string Country, DateOnly? Date, int? DisplayOrder);
