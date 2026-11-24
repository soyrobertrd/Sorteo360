using Sorteo360.Convocatorias.Core.Shared;

namespace Sorteo360.Convocatorias.Core.CheckIns;

/// <summary>
/// La lista de acceso de un evento (spec <c>09</c> §5.1): sus inscripciones tal como las ve la
/// puerta, para encontrar y hacer pasar a mano a quien no tiene un QR que mostrar.
/// </summary>
/// <param name="Totals">Los del evento entero, sin filtro: el pie del escáner.</param>
/// <param name="Page">La página pedida, ya filtrada y ordenada.</param>
public sealed record DoorList(DoorListTotals Totals, PagedResult<DoorListRow> Page);

/// <summary>
/// Cuántas inscripciones tiene el evento y cuántas tienen check-in. No mira la búsqueda ni el
/// filtro: es el <i>Entradas: 287/412</i> del pie, y no cambia con lo que se teclee en la caja.
/// </summary>
public sealed record DoorListTotals(int Registered, int CheckedIn);

/// <summary>
/// Una inscripción del evento para la puerta.
/// <para>
/// <see cref="EmailNormalized"/> está para buscar y para nada más: <b>ninguna respuesta de la
/// puerta lleva un correo</b> (§5.1), así que la forma de la respuesta no tiene dónde ponerlo.
/// </para>
/// </summary>
/// <param name="EmailNormalized">La dirección recortada y en minúsculas, como la guarda la columna.</param>
/// <param name="CheckedInAt">Cuándo entró, o nulo si todavía no.</param>
public sealed record DoorListRow(
    Guid RegistrationId,
    string FirstName,
    string LastName,
    string? Company,
    string Reference,
    string EmailNormalized,
    DateTimeOffset? CheckedInAt);
