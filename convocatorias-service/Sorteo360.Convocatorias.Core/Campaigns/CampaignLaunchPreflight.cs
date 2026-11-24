namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Lo que se lee de una campaña antes de tocar el CRM: su estado, qué evento convoca y a quién
/// pretende escribirle. Es lo que hace posible el orden de la spec <c>04</c> §1, donde validar es
/// el paso 1, los dos del CRM son el 2 y el 3, y leer el evento es el 4.
/// <para>
/// <b>No es la autoridad.</b> Las guardas de verdad corren dentro de la transacción que escribe,
/// porque leer el estado antes y decidir después dejaría pasar dos lanzamientos simultáneos sobre
/// el mismo borrador. Lo que esta lectura evita es empujarle bajas al CRM por una campaña que ya
/// se sabe que va a ser rechazada.
/// </para>
/// </summary>
/// <param name="State">Ver <see cref="CampaignState"/>.</param>
/// <param name="EventId">
/// El evento que se convoca. Se lee acá para que el paso 4 del orden —leer el evento, rechazar
/// el cancelado y el valor que falta— no tenga que volver a la tabla por una sola columna. Nulo en
/// un mensaje directo, que se salta los pasos 4 y 5: no hay evento que leer ni ventana que mirar.
/// </param>
/// <param name="TemplateKey">La plantilla que nombra la campaña.</param>
/// <param name="Language">El idioma en el que la va a mandar.</param>
/// <param name="HasTemplate">
/// Si esa pareja está sembrada. Se lee acá, antes del CRM, porque una campaña sin plantilla en su
/// idioma no se puede armar: sin esta lectura el lanzamiento contestaba <c>202</c> y el worker se
/// quedaba atascado en <c>sending</c> (spec <c>03</c> §4.1).
/// </param>
/// <param name="RecipientContactIds">
/// La identidad de cada destinatario, que es por quienes se le vuelve a preguntar al CRM. Su
/// cuenta es la que se contrasta con la que trae quien lanza.
/// </param>
public sealed record CampaignLaunchPreflight(
    string State,
    Guid? EventId,
    string TemplateKey,
    string Language,
    bool HasTemplate,
    IReadOnlyList<Guid> RecipientContactIds);
