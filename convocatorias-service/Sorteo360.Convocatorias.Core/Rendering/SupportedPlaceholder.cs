namespace Sorteo360.Convocatorias.Core.Rendering;

/// <summary>
/// El conjunto de marcadores que una plantilla puede nombrar. Son dos cosas que se confunden
/// fácil: <b>el conjunto soportado es código</b>, porque rellenar una variable es código, y
/// <b>el conjunto usado se escanea del cuerpo</b>. Nadie escribe una lista de marcadores en un
/// formulario y no existe ninguna columna que la guarde.
/// <para>
/// La regla que sale de ahí: todo marcador de un cuerpo tiene que estar acá. Un
/// <c>{{evneto}}</c> rompe la compilación en vez de llegar a mil bandejas de entrada.
/// </para>
/// <para>
/// <b>Los nombres son ingleses y los valores españoles, y eso no es una inconsistencia.</b> El
/// identificador lo ve el sistema de tipos, así que es inglés (ADR-0008). El valor lo escribe a
/// mano quien redacta la copia, dentro de un correo en español, y viaja hasta
/// notification-service como clave de sustitución: es dialecto de plantilla, está fijado por
/// <c>02</c> §2, y traducirlo rompería toda plantilla ya escrita.
/// </para>
/// </summary>
public static class SupportedPlaceholder
{
    /// <summary>Nombre de pila de la persona.</summary>
    public const string RecipientName = "nombre";

    /// <summary>Nombre del evento.</summary>
    public const string EventName = "evento";

    /// <summary>Ciudades del roadshow, nombre y país, unidas.</summary>
    public const string EventLocation = "lugar";

    /// <summary>Fechas del evento.</summary>
    public const string EventDates = "fecha";

    /// <summary>Enlace de registro, con el token de esa persona.</summary>
    public const string RegistrationLink = "enlace_registro";

    /// <summary>URL de la imagen del código QR de esa persona.</summary>
    public const string QrImageUrl = "qr";

    /// <summary>Enlace de baja, con el token de esa persona.</summary>
    public const string UnsubscribeLink = "enlace_baja";

    /// <summary>
    /// La referencia de la inscripción, en texto. Se imprime en la escarapela y se dicta por
    /// teléfono; no autoriza nada (spec <c>06</c> §2.3). La lleva el correo de confirmación.
    /// </summary>
    public const string Reference = "referencia";

    /// <summary>
    /// El enlace a la página de la entrada de esa persona. La lleva el correo de confirmación
    /// junto al QR, para quien prefiere un enlace a una imagen que su cliente de correo puede
    /// bloquear (spec <c>06</c> §4.4 y §4.5).
    /// </summary>
    public const string TicketLink = "enlace_entrada";

    /// <summary>El asunto de un mensaje directo, tal como lo escribió quien armó la campaña.</summary>
    public const string DirectSubject = "asunto";

    /// <summary>
    /// El texto de un mensaje directo. Es texto plano: se escapa como cualquier valor, y la
    /// plantilla conserva sus saltos de línea con <c>white-space:pre-line</c>.
    /// </summary>
    public const string DirectMessage = "mensaje";

    /// <summary>
    /// Saludo neutro con el que se rellena <see cref="RecipientName"/> cuando la fila importada
    /// solo trae una dirección de correo. Saltarse a esas personas se descartó: dejaría fuera, en
    /// silencio, a gente que la coordinadora cree invitada.
    /// </summary>
    public const string NeutralRecipientName = "invitado/a";

    /// <summary>
    /// Cada marcador soportado con su grano. El grano es lo que decide qué pasa cuando falta el
    /// valor, y son dos políticas distintas a propósito (<see cref="PlaceholderGrain"/>).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, PlaceholderGrain> All =
        new Dictionary<string, PlaceholderGrain>(StringComparer.Ordinal)
        {
            [RecipientName]    = PlaceholderGrain.Recipient,
            [EventName]        = PlaceholderGrain.Event,
            [EventLocation]    = PlaceholderGrain.Event,
            [EventDates]       = PlaceholderGrain.Event,
            [RegistrationLink] = PlaceholderGrain.Recipient,
            [QrImageUrl]       = PlaceholderGrain.Recipient,
            [UnsubscribeLink]  = PlaceholderGrain.Recipient,
            [Reference]        = PlaceholderGrain.Recipient,
            [TicketLink]       = PlaceholderGrain.Recipient,
            [DirectSubject]    = PlaceholderGrain.Campaign,
            [DirectMessage]    = PlaceholderGrain.Campaign,
        };

    /// <summary>Si la clave está soportada.</summary>
    public static bool IsSupported(string key) => All.ContainsKey(key);

    /// <summary>
    /// Marcadores cuyo valor sale del evento. Es el único grano que se pregunta desde fuera: el
    /// de persona no rechaza nada, así que no hay a quién darle la lista.
    /// </summary>
    public static IReadOnlySet<string> EventLevel { get; } =
        All.Where(entry => entry.Value == PlaceholderGrain.Event)
           .Select(entry => entry.Key)
           .ToHashSet(StringComparer.Ordinal);
}
