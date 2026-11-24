namespace Sorteo360.Notifications.Core.DTOs.History;

/// <summary>
/// Un envío entero: lo que se escribió, de qué trataba y a quién se le mandó.
/// <para>
/// <c>Body</c> es el cuerpo <b>sin sustituir</b>, con sus <c>{{placeholders}}</c> intactos, porque
/// es lo único que existe: los valores de <c>data</c> no se guardan. Es también la razón de que no
/// haya reenvío, y de que este endpoint sea de solo lectura como el resto.
/// </para>
/// </summary>
public sealed record HistoryBatchDto(
    Guid Id,
    string Type,
    string Subject,
    string Body,
    string Language,
    string FromAddress,
    string? FromName,
    bool DeliveryTrackingAvailable,
    string BodySha256,
    DateTimeOffset CreatedAt,
    IReadOnlyList<HistoryAboutDto> About,
    IReadOnlyList<HistoryRecipientDto> Recipients);
