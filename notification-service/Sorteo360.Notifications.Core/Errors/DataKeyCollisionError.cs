using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Core.Errors;

/// <summary>
/// Una clave que viene a la vez en los datos comunes y en los de una persona. Es un 400 y nunca
/// una regla de precedencia: no hay motivo legítimo para mandar el mismo valor dos veces, y un
/// ganador silencioso genera bugs. Lleva la clave dentro de la respuesta para que quien llama
/// sepa cuál quitar.
/// </summary>
public sealed record DataKeyCollisionError : BaseError
{
    public const string ErrorCode = "NOTIFICATION_DATA_KEY_COLLISION";

    /// <summary>Clave de la extensión ProblemDetails que lleva la clave repetida.</summary>
    private const string DataKeyExtension = "dataKey";

    /// <summary>Clave que llega dos veces.</summary>
    public string DataKey { get; init; }

    /// <summary>Dirección de la persona en cuyos datos se repite.</summary>
    public string Recipient { get; init; }

    public DataKeyCollisionError(string dataKey, string recipient)
        : base(
            $"La clave '{dataKey}' viene en sharedData y también en los datos de {recipient}; déjela en uno de los dos.",
            ErrorCode,
            ErrorKind.Invalid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

        DataKey = dataKey;
        Recipient = recipient;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [DataKeyExtension] = DataKey
        };
}
