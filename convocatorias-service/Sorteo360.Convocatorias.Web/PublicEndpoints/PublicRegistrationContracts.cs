namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>
/// Abrir un enlace de invitación. <b>Es un <c>POST</c> y el token va en el cuerpo</b>, no en la
/// URL (spec <c>06</c> §2.1): nginx y Traefik anotan la línea de petición entera en su registro
/// de acceso, en disco, en stage y en la red que el cliente tenga en producción.
/// </summary>
public sealed record ResolveInvitationRequest
{
    /// <summary>El token del enlace, que el portal lee del fragmento <c>#t=</c>.</summary>
    public string? Token { get; init; }
}

/// <summary>
/// El formulario de inscripción (spec <c>06</c> §4.1). Siete campos, y el token aparte.
/// </summary>
public sealed record RegisterRequest
{
    /// <summary>A qué evento.</summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// El token, <b>opcional</b>. Sin él la inscripción se acepta solo si el evento es público
    /// y no queda referencia a ninguna campaña (spec <c>06</c> §1.1).
    /// </summary>
    public string? Token { get; init; }

    /// <summary>Su nombre. Separado del apellido y no partido al enviar.</summary>
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>Texto libre: no se resuelve contra ninguna empresa del CRM.</summary>
    public string? Company { get; init; }

    public string? JobTitle { get; init; }

    public string? Country { get; init; }
}

/// <summary>
/// Lo que hay detrás de un enlace: el evento, y los datos ya escritos solo en un evento privado
/// (spec <c>06</c> §2.2).
/// </summary>
public sealed record ResolvedInvitationResponse
{
    public required PublicEventDetailResponse Event { get; init; }

    /// <summary>
    /// <b>Nulo en un evento público, siempre.</b> Su token está pensado para reenviarse, así que
    /// rellenar sobre un enlace reenviado le enseñaría a quien lo recibe el nombre y la dirección
    /// del invitado original.
    /// </summary>
    public InvitationPrefillResponse? Prefill { get; init; }
}

/// <summary>Los tres campos que el portal deja escritos. No hay más en la invitación.</summary>
public sealed record InvitationPrefillResponse
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }
}

/// <summary>
/// La entrada, tal como la ve quien la abre con su <c>qrCode</c> (spec <c>06</c> §2.3).
/// <para>
/// <b>Solo lo impreso en la escarapela</b>: la referencia, el evento, el nombre y la empresa.
/// Nada del CRM, ni el destinatario que la trajo, ni la dirección de correo: sacar cualquiera de
/// esos por una ruta pública sería filtrarlo.
/// </para>
/// </summary>
public sealed record RegistrationTicketResponse
{
    /// <summary>
    /// <c>MITUR-&lt;año&gt;-&lt;secuencia&gt;</c>. Se imprime en la escarapela y se dicta por
    /// teléfono, y <b>no autoriza nada</b>.
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>El nombre completo con el que la persona se inscribió.</summary>
    public required string Name { get; init; }

    /// <summary>La empresa que escribió, o nulo: es texto libre y opcional.</summary>
    public string? Company { get; init; }

    /// <summary>El evento al que da entrada, en el mismo recorte que la cartelera.</summary>
    public required PublicEventDetailResponse Event { get; init; }
}

/// <summary>
/// La inscripción, tal como la ve quien acaba de mandar el formulario.
/// </summary>
public sealed record RegistrationResponse
{
    /// <summary>
    /// <c>MITUR-&lt;año&gt;-&lt;secuencia&gt;</c>. Se imprime en la escarapela y se dicta por
    /// teléfono, y <b>no autoriza nada</b>.
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>
    /// Lo que codifica el QR y la clave de su URL. El portal lo usa para pedir la imagen y la
    /// página de la entrada; <b>no se enseña como texto</b>.
    /// </summary>
    public required string QrCode { get; init; }

    public required PublicEventDetailResponse Event { get; init; }
}
