using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// El lote pasa del tope de mil que declara el contrato (spec <c>04</c> §2.2).
/// <para>
/// <see cref="ErrorKind.Invalid"/>, que es el 400 con el que contesta notification-service: no hay
/// nada en el estado de la campaña que lo impida, lo que no sirve es el lote que llegó. Que se
/// dispare es un fallo de este lado, porque trocear es trabajo de quien llama.
/// </para>
/// </summary>
public sealed record NotificationBatchTooLargeError : BaseError
{
    public const string ErrorCode = "NOTIFICATION_BATCH_TOO_LARGE";

    private const string CountExtension = "recipientCount";

    /// <summary>Cuántos destinatarios traía el lote.</summary>
    public int RecipientCount { get; init; }

    /// <summary>
    /// El tope lo pasa quien lo cobra, en vez de leerlo de <c>NotificationBatchContract</c>: así
    /// este archivo no depende de <c>Core.Campaigns</c>, que ya depende de él.
    /// </summary>
    public NotificationBatchTooLargeError(int recipientCount, int maxRecipients)
        : base(
            $"El lote trae {recipientCount} destinatarios y el tope por llamada es {maxRecipients}.",
            ErrorCode,
            ErrorKind.Invalid)
    {
        RecipientCount = recipientCount;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CountExtension] = RecipientCount
        };
}

/// <summary>
/// La misma clave viaja en la bolsa compartida y en la de una persona (spec <c>04</c> §2).
/// <para>
/// Nunca se resuelve con una regla de precedencia: un ganador silencioso es un generador de
/// fallos, así que el lote entero se rechaza y la clave se nombra.
/// </para>
/// </summary>
public sealed record NotificationDataKeyConflictError : BaseError
{
    public const string ErrorCode = "NOTIFICATION_DATA_KEY_CONFLICT";

    private const string KeyExtension = "key";

    /// <summary>La clave que está en las dos bolsas.</summary>
    public string Key { get; init; }

    public NotificationDataKeyConflictError(string key)
        : base(
            $"La clave '{key}' viaja en la bolsa compartida y en la de un destinatario: "
            + "no hay precedencia, el lote se rechaza.",
            ErrorCode,
            ErrorKind.Invalid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Key = key;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [KeyExtension] = Key
        };
}

/// <summary>
/// notification-service contestó un <c>202</c> que no se puede emparejar con el lote que salió:
/// otra cantidad de resultados, otra dirección en una posición, o un estado que no es del contrato
/// (spec <c>04</c> §2.1).
/// <para>
/// <b>Tumba el lote entero en vez de anotar la parte que sí se entiende.</b> Media tanda anotada
/// contra una respuesta que no se entiende es peor que la cola intacta que reanuda una persona.
/// </para>
/// <para>
/// <see cref="ErrorKind.Unavailable"/>, como una caída: este servicio está sano y quien falla es
/// aquello de lo que depende. Lleva código propio porque no es lo mismo que no contestar, y quien
/// lea el log tiene que poder distinguir "estaba caído" de "contestó cualquier cosa".
/// </para>
/// </summary>
/// <param name="Detail">Qué no cuadró, para el log.</param>
public sealed record NotificationBatchResponseInvalidError(string Detail)
    : BaseError(
        $"notification-service contestó algo que no es el contrato: {Detail}",
        ErrorCode,
        ErrorKind.Unavailable)
{
    public const string ErrorCode = "NOTIFICATION_BATCH_RESPONSE_INVALID";
}
