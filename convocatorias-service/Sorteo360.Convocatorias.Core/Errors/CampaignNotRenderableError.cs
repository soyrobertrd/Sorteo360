using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La campaña salió del canal y no hay con qué armar su correo: le falta la plantilla de su
/// idioma, o los tres valores del evento que el lanzamiento tenía que haberle copiado.
/// <para>
/// <b>Nadie lo ve como respuesta HTTP.</b> Ocurre en el worker, después del <c>202</c> y sin
/// navegador detrás, así que este error es lo que queda en el log y lo que deja las filas
/// <c>pending</c> para que una persona reanude. Preferirlo a mandar el correo con un hueco es la
/// misma decisión que toma el paso 4 del lanzamiento, un paso más tarde.
/// </para>
/// </summary>
public sealed record CampaignNotRenderableError(string Detail)
    : BaseError(
        $"No hay con qué armar el correo de la campaña: {Detail}",
        ErrorCode,
        ErrorKind.Internal)
{
    public const string ErrorCode = "CAMPAIGN_NOT_RENDERABLE";
}
