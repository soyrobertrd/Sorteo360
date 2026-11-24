using Sorteo360.Notifications.Core.DTOs;

namespace Sorteo360.Notifications.Tests.Unit.Notifications;

/// <summary>
/// Peticiones de prueba armadas a mano, para que cada test cambie solo lo que está probando.
/// </summary>
internal static class NotificationTestData
{
    public const string Key = "events:quote:abc-123:attempt:2:submitted";

    public static SendNotificationDto Send(
        string? subject = null,
        string? body = null,
        IReadOnlyDictionary<string, string>? sharedData = null,
        IReadOnlyList<NotificationRecipientDto>? recipients = null,
        IReadOnlyList<NotificationAboutDto>? about = null,
        string idempotencyKey = Key) =>
        new(
            Type: "events.quote.submitted",
            Language: "es",
            FromAddress: "no-reply@sorteo360.local",
            FromName: "MITUR",
            Subject: subject ?? "Cotización pendiente",
            Body: body ?? "<p>Hola</p>",
            SharedData: sharedData ?? Data(),
            Recipients: recipients ?? [Recipient("maria@sorteo360.local")],
            About: about ?? [new NotificationAboutDto("events", "event", "xyz-789")],
            IdempotencyKey: idempotencyKey,
            TraceId: "00000000000000000000000000000001");

    public static NotificationRecipientDto Recipient(string email, params (string Key, string Value)[] data) =>
        new(email, null, Data(data));

    public static NotificationRecipientDto Named(string email, string name, params (string Key, string Value)[] data) =>
        new(email, name, Data(data));

    public static IReadOnlyDictionary<string, string> Data(params (string Key, string Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
}
