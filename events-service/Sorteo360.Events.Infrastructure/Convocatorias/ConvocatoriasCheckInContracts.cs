namespace Sorteo360.Events.Infrastructure.Convocatorias;

/// <summary>
/// La lista de check-ins de convocatorias (spec <c>09</c> §7), envuelta en <c>items</c>. Anulable a
/// propósito: un cuerpo sin <c>items</c> es una respuesta ilegible, no una puerta vacía.
/// </summary>
public sealed record ConvocatoriasCheckInList(IReadOnlyList<ConvocatoriasCheckIn>? Items);

/// <summary>
/// Una fila de esa lista. <c>registrationId</c> viaja pero no se lee: la identidad del sorteo es la
/// dirección normalizada (spec 01 §2). Los campos que toda fila trae son anulables por lo mismo que
/// <c>Items</c>: una fila sin ellos es ilegible, y como valor por omisión pasaría por buena.
/// </summary>
public sealed record ConvocatoriasCheckIn(
    string? EmailNormalized,
    string? FirstName,
    string? LastName,
    string? Company,
    Guid? ContactId,
    string? Origin,
    DateTimeOffset? CheckedInAt);
