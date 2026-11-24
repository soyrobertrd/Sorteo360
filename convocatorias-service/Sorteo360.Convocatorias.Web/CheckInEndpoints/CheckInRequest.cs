namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// El escaneo: el código pelado, sin la URL de la entrada alrededor. Quitarla es trabajo del
/// escáner (spec <c>09</c> §3); acá no se interpreta la forma de las rutas del portal.
/// </summary>
public sealed record CheckInRequest
{
    /// <summary>El código QR de la entrada, de 1 a <see cref="Validators.CheckInRequestValidator.MaxQrCodeLength"/> caracteres.</summary>
    public string? QrCode { get; init; }
}
