using System.Text.Json;

namespace Sorteo360.Notifications.Core.Webhooks;

/// <summary>
/// El parseo del lote, que corre <b>después</b> de verificar la firma y nunca antes.
/// <para>
/// El proveedor postea un array de objetos. Los <c>unique_args</c> que mandamos en
/// <c>X-SMTPAPI</c> vuelven aplanados como campos de primer nivel del evento, así que
/// <c>recipient_id</c> se busca arriba; se acepta además anidado bajo <c>unique_args</c> por si
/// alguna ruta del proveedor lo devuelve como se mandó.
/// </para>
/// </summary>
public static class SendGridEventBatch
{
    /// <summary>La clave de nuestro identificador opaco, la misma que se manda en la cabecera.</summary>
    private const string CorrelationField = "recipient_id";

    /// <summary>
    /// Los eventos del lote, o <c>null</c> si el cuerpo no es un lote.
    /// <para>
    /// Un lote mal formado se rechaza entero y no a medias: el reintento de 24 horas del proveedor
    /// lo vuelve a traer, así que un 400 visible es preferible a guardar la mitad y perder el
    /// resto en silencio. Faltar <c>event</c> o <c>timestamp</c> cuenta como mal formado; un
    /// <c>event</c> que no reconocemos, no.
    /// </para>
    /// </summary>
    public static IReadOnlyList<SendGridEvent>? TryParse(string rawBody)
    {
        ArgumentNullException.ThrowIfNull(rawBody);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            var events = new List<SendGridEvent>();

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                var parsed = TryParseEntry(entry);

                if (parsed is null)
                    return null;

                events.Add(parsed);
            }

            return events;
        }
    }

    private static SendGridEvent? TryParseEntry(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
            return null;

        if (Text(entry, "event") is not { Length: > 0 } eventType)
            return null;

        if (!entry.TryGetProperty("timestamp", out var timestamp)
            || timestamp.ValueKind != JsonValueKind.Number
            || !timestamp.TryGetInt64(out var occurredAt))
        {
            return null;
        }

        return new SendGridEvent(
            EventType: eventType,
            Email: Text(entry, "email") ?? string.Empty,
            Timestamp: occurredAt,
            SgEventId: Text(entry, "sg_event_id"),
            SgMessageId: Text(entry, "sg_message_id"),
            CorrelationId: CorrelationIdOf(entry),
            Payload: entry.GetRawText());
    }

    /// <summary>
    /// Nuestro GUID: primero aplanado en el primer nivel, que es como el proveedor lo devuelve, y
    /// si no, anidado bajo <c>unique_args</c>. Un valor que no es un GUID se trata como ausente,
    /// y el evento cae al segundo peldaño de la escalera en vez de romper el lote.
    /// </summary>
    private static Guid? CorrelationIdOf(JsonElement entry)
    {
        if (Guid.TryParse(Text(entry, CorrelationField), out var flat))
            return flat;

        if (entry.TryGetProperty("unique_args", out var uniqueArgs)
            && uniqueArgs.ValueKind == JsonValueKind.Object
            && Guid.TryParse(Text(uniqueArgs, CorrelationField), out var nested))
        {
            return nested;
        }

        return null;
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
