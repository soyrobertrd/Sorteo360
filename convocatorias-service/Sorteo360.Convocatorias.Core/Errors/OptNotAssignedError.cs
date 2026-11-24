using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// Quien llama no tiene una OPT, o la suya está cerrada, y las listas de contactos son de cada
/// OPT. Es el mismo código que devuelve el CRM en sus rutas de listas, para que la pantalla
/// ramifique igual venga de donde venga. Un administrador central no tiene OPT y por eso no usa
/// listas.
/// </summary>
public sealed record OptNotAssignedError()
    : BaseError(
        "Su usuario no tiene una OPT asignada; contacte al administrador.",
        ErrorCode,
        ErrorKind.Forbidden)
{
    public const string ErrorCode = "OPT_NOT_ASSIGNED";
}
