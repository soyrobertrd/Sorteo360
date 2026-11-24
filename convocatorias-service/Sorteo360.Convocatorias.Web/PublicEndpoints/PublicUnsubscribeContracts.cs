namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>
/// Darse de baja. <b>Es un <c>POST</c> con el token en el cuerpo</b>, no un <c>GET</c> (spec
/// <c>06</c> §2.4): el clic solo abre la página de confirmación, y un escáner corporativo que
/// precarga los enlaces de un correo no debe dar de baja a nadie en silencio. El token va en el
/// cuerpo por lo mismo que en resolver: la URL acaba en registros de acceso, el cuerpo no.
/// </summary>
public sealed record UnsubscribeRequest
{
    /// <summary>El token de baja, que el portal lee del fragmento <c>#u=</c>.</summary>
    public string? Token { get; init; }
}

/// <summary>
/// Lo que confirma la baja: la dirección sobre la que actuó, enmascarada (spec <c>06</c> §2.4).
/// <b>Enmascarada a propósito</b>: quien recibió un reenvío ve una dirección que no es la suya y
/// se detiene, sin que nunca se enseñe la completa.
/// </summary>
public sealed record UnsubscribeResponse
{
    /// <summary>La dirección dada de baja, como <c>a***@acme.com</c>.</summary>
    public required string MaskedEmail { get; init; }
}
