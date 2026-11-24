using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Errors;

/// <summary>
/// Errores de edición vinculados al estado terminal del evento.
/// </summary>
public static class QuoteErrors
{
    /// <summary>
    /// El evento llegó a un estado terminal (cerrado o cancelado) y ya no admite cambios.
    /// </summary>
    public static BaseError EventNoEditable() => new(
        "El evento ya está cerrado o cancelado y no admite cambios.",
        "EVENT_NOT_EDITABLE",
        ErrorKind.Conflict);
}
