using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La lista de contactos no existe, o es de otra OPT. <b>Un solo código para los dos casos</b>,
/// como hace el CRM con sus propias lecturas: decirle a alguien que la lista existe pero no es
/// suya ya le cuenta algo de otra oficina.
/// </summary>
public sealed record ContactListNotFoundError()
    : BaseError(
        "La lista de contactos no fue encontrada.",
        ErrorCode,
        ErrorKind.NotFound)
{
    public const string ErrorCode = "CONTACT_LIST_NOT_FOUND";
}
