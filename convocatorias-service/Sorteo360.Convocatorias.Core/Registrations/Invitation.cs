using Sorteo360.Convocatorias.Core.Events;

namespace Sorteo360.Convocatorias.Core.Registrations;

/// <summary>
/// A quién apuntaba un token de registro: la fila de <c>campaign_recipient</c> que lo guarda,
/// reducida a lo que el portal necesita.
/// <para>
/// Lleva el hash que de verdad está guardado, y no solo el id, porque quien resuelve tiene que
/// volver a compararlo en tiempo fijo (spec <c>06</c> §2.1). El índice es lo que <i>encuentra</i>
/// la fila; la comparación es lo que la <i>acepta</i>.
/// </para>
/// </summary>
/// <param name="RecipientId">El id de la fila, que es lo que se anota en la inscripción.</param>
/// <param name="EventId">El evento de su campaña. Un token no sirve para otro.</param>
/// <param name="TokenHash">El hash guardado, para la comparación en tiempo fijo.</param>
/// <param name="FirstName">Su nombre, tal como se le escribió.</param>
/// <param name="LastName">Su apellido.</param>
/// <param name="Email">La dirección a la que le llegó la invitación.</param>
public sealed record Invitation(
    Guid RecipientId,
    Guid EventId,
    byte[] TokenHash,
    string FirstName,
    string LastName,
    string Email);

/// <summary>
/// Lo que ve quien abre su enlace: el evento, y sus datos ya escritos <b>solo si el evento es
/// privado</b> (spec <c>06</c> §2.2).
/// <para>
/// <b>El reparto es la decisión entera.</b> Un token privado está atado a una sola persona y
/// llegó a su dirección, así que devolverle su nombre no le enseña nada que no supiera. Uno
/// público está pensado para reenviarse, y rellenar sobre un enlace reenviado le enseñaría a
/// quien lo recibe el nombre y la dirección del invitado original.
/// </para>
/// </summary>
/// <param name="Event">El evento al que apunta el enlace.</param>
/// <param name="Prefill">Los datos del invitado, o nulo en un evento público.</param>
public sealed record ResolvedInvitation(EventSummary Event, InvitationPrefill? Prefill);

/// <summary>
/// Los tres campos que el portal deja ya escritos en el formulario. <b>Nada más</b>: el resto
/// —teléfono, empresa, cargo, país— no está en <c>campaign_recipient</c> y adivinarlo sería
/// inventarlo.
/// </summary>
/// <param name="FirstName">Su nombre.</param>
/// <param name="LastName">Su apellido.</param>
/// <param name="Email">Su dirección.</param>
public sealed record InvitationPrefill(string FirstName, string LastName, string Email);

/// <summary>
/// Lo que sale de mandar el formulario: la inscripción y si es nueva.
/// <para>
/// <see cref="IsNew"/> es lo que separa un <c>201</c> de un <c>200</c>, y existe porque un
/// reenvío del mismo formulario <b>no es un error</b> (spec <c>06</c> §4.1): devuelve la
/// inscripción que ya había, con sus mismos códigos, sin crear fila y sin pisar lo que la
/// persona escribió la primera vez.
/// </para>
/// </summary>
/// <param name="Registration">La inscripción, nueva o la que ya estaba.</param>
/// <param name="Event">El evento, que viaja en la respuesta.</param>
/// <param name="IsNew">Si esta petición fue la que la creó.</param>
public sealed record RegistrationOutcome(Registration Registration, EventSummary Event, bool IsNew);
