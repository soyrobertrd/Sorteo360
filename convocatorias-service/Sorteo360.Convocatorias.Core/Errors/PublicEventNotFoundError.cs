using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// El portal no tiene nada que enseñar en ese id. <b>Un solo código para los cuatro motivos</b>
/// —no existe, es privado, fue cancelado, o el id está mal escrito— y a propósito: decirle a un
/// desconocido cuál de los cuatro es le confirma que el evento existe y que MITUR lo escondió
/// (spec <c>01</c> §5, spec <c>06</c> §4.6).
/// </summary>
public sealed record PublicEventNotFoundError()
    : BaseError(
        "No se encontró el evento.",
        ErrorCode,
        ErrorKind.NotFound)
{
    public const string ErrorCode = "PUBLIC_EVENT_NOT_FOUND";
}
