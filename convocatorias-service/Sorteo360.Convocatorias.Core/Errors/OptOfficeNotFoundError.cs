using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La oficina para la que se pide un enlace de carga de proveedores no está en el directorio de OPT
/// del CRM, o está cerrada.
/// </summary>
/// <remarks>
/// Un solo código para los dos casos: una oficina cerrada no tiene personal que pueda usar el
/// enlace, así que para quien lo pide es lo mismo que una que no existe.
/// </remarks>
public sealed record OptOfficeNotFoundError()
    : BaseError(
        "La oficina no existe en el directorio de OPT o está cerrada.",
        ErrorCode,
        ErrorKind.NotFound)
{
    public const string ErrorCode = "OPT_OFFICE_NOT_FOUND";
}
