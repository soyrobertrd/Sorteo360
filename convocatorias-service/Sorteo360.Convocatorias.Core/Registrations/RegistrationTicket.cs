using Sorteo360.Convocatorias.Core.Events;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// La entrada, tal como la ve quien la abre con su <c>qr_code</c> (spec <c>06</c> §2.3 y §4.5).
/// <para>
/// <b>Es lectura pura y no lleva nada más que lo impreso en la escarapela</b>: la referencia, el
/// evento, el nombre y la empresa. Ni el estado de sincronización con el CRM, ni el destinatario
/// que la trajo, ni la dirección de correo: nada de eso le importa a quien enseña su entrada en
/// la puerta, y sacarlo por una ruta pública sería filtrarlo.
/// </para>
/// <para>
/// <b>El evento se lee en vivo</b> y no se guarda: es de events-service, igual que en la
/// cartelera. Sigue sirviendo aunque el evento esté cancelado o cerrado, porque quien ya tiene su
/// entrada puede seguir mirándola (spec <c>06</c> §4.6).
/// </para>
/// </summary>
/// <param name="Reference">El código humano, impreso y dictado por teléfono. No autoriza nada.</param>
/// <param name="Event">El evento al que da entrada, en el mismo recorte que la cartelera.</param>
/// <param name="FullName">El nombre completo con el que la persona se inscribió.</param>
/// <param name="Company">La empresa que escribió, o nulo: es texto libre y opcional.</param>
public sealed record RegistrationTicket(string Reference, EventSummary Event, string FullName, string? Company);
