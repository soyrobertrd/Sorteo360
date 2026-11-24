namespace Sorteo360.Notifications.Core.Notifications;

/// <summary>
/// Un POST aceptado: el correo tal como lo escribió quien llama, más lo que hace falta para
/// reconocerlo si vuelve a llegar.
/// <para>
/// El cuerpo se guarda <b>antes</b> de sustituir, todavía con sus <c>{{placeholders}}</c>. Los
/// valores no se guardan en ninguna parte, así que un token de registro que viaje en
/// <c>data</c> no puede terminar dentro de un cuerpo almacenado: la garantía es estructural y no
/// una regla de limpieza que alguien pueda debilitar después.
/// </para>
/// </summary>
public sealed record NotificationBatch
{
    public Guid Id { get; init; }

    /// <summary>Etiqueta de historial, verbatim. No hay catálogo ni clave ajena detrás.</summary>
    public string Type { get; init; }

    public string Language { get; init; }
    public string FromAddress { get; init; }
    public string? FromName { get; init; }
    public string Subject { get; init; }

    /// <summary>Cuerpo HTML completo, sin sustituir.</summary>
    public string Body { get; init; }

    /// <summary>Clave de idempotencia acuñada por quien llama. Única en todo el servicio.</summary>
    public string IdempotencyKey { get; init; }

    /// <summary>SHA-256 de la petición canónica, para delatar la misma clave con otro contenido.</summary>
    public string BodySha256 { get; init; }

    /// <summary>
    /// Si existía el webhook de eventos cuando esta fila se escribió. Se estampa al insertar y no
    /// cambia nunca: una fila escrita sin webhook sigue siendo honesta años después, aunque
    /// alguien encienda el webhook más tarde.
    /// </summary>
    public bool TrackingExpected { get; init; }

    public string? TraceId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public NotificationBatch(
        Guid id,
        string type,
        string language,
        string fromAddress,
        string? fromName,
        string subject,
        string body,
        string idempotencyKey,
        string bodySha256,
        bool trackingExpected,
        string? traceId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodySha256);

        Id = id;
        Type = type;
        Language = language;
        FromAddress = fromAddress;
        FromName = fromName;
        Subject = subject;
        Body = body;
        IdempotencyKey = idempotencyKey;
        BodySha256 = bodySha256;
        TrackingExpected = trackingExpected;
        TraceId = traceId;
        CreatedAt = createdAt;
    }
}
