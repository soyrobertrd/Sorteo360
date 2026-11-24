using Microsoft.AspNetCore.Http;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Platform.GlobalEndpoints;

/// <summary>
/// Traduce un <see cref="BaseError"/> de dominio a la respuesta HTTP correspondiente.
/// Es el único lugar donde vive la tabla <see cref="ErrorKind"/> → estado HTTP; los
/// endpoints invocan <see cref="ToProblem"/> explícitamente en la rama de fallo del
/// patrón Result (no es un manejador global de excepciones).
/// </summary>
public static class BaseErrorExtensions
{
    private const string CodeExtension = "code";

    /// <summary>
    /// Convierte el error en un <see cref="IResult"/> de tipo ProblemDetails, mapeando
    /// su <see cref="BaseError.Kind"/> al estado HTTP: NotFound → 404, Invalid → 400,
    /// Forbidden → 403, Conflict → 409, Unavailable → 503, Unauthenticated → 401,
    /// TooManyRequests → 429, y cualquier otro caso
    /// (incluido Internal) → 500.
    /// <para>
    /// El <see cref="BaseError.Code"/> viaja como extensión <c>code</c> cuando existe. Es lo que
    /// permite distinguir dos fallos que comparten estado —dos 409 por motivos distintos— sin leer
    /// el mensaje, que es prosa en español pensada para una persona y no un identificador. Quien
    /// consume decide qué decir y en qué idioma a partir del código.
    /// </para>
    /// <para>
    /// Junto al código viajan las extensiones que el propio error aporta en
    /// <see cref="BaseError.ProblemExtensions"/>, así que un fallo puede llevar el dato con el que
    /// quien consume lo resuelve sin que el endpoint arme la respuesta a mano. El <c>code</c> es del
    /// mapeador y una subclase no lo sobrescribe.
    /// </para>
    /// </summary>
    public static IResult ToProblem(this BaseError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var statusCode = error.Kind switch
        {
            ErrorKind.NotFound    => StatusCodes.Status404NotFound,
            ErrorKind.Invalid     => StatusCodes.Status400BadRequest,
            ErrorKind.Forbidden   => StatusCodes.Status403Forbidden,
            ErrorKind.Conflict    => StatusCodes.Status409Conflict,
            ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
            ErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
            ErrorKind.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError
        };

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (key, value) in error.ProblemExtensions)
        {
            // El código lo pone el mapeador desde Code y nadie más: una subclase que traiga la
            // clave no puede suplantar el identificador por el que rama quien consume.
            if (!string.Equals(key, CodeExtension, StringComparison.Ordinal))
                extensions[key] = value;
        }

        // Sin código no se emite la clave: una extensión con valor nulo obligaría a quien consume a
        // distinguir "ausente" de "nulo" para nada.
        if (error.Code is not null)
            extensions[CodeExtension] = error.Code;

        return Results.Problem(
            error.Message,
            statusCode: statusCode,
            extensions: extensions.Count == 0 ? null : extensions);
    }
}
