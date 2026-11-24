using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Core.Errors;

/// <summary>
/// Un <c>{{marcador}}</c> del asunto o del cuerpo sin valor en ninguna parte. Rechaza la petición
/// entera nombrando la clave: después del 202 no queda a quién avisar, así que este es el último
/// momento en que el fallo le puede llegar a quien lo puede arreglar.
/// <para>
/// El nombre de la clave no es asunto de este servicio: no tiene ni valida contra ninguna lista de
/// claves legales. Solo pregunta si la que el propio cuerpo nombra tiene un valor que quien llama
/// también mandó.
/// </para>
/// </summary>
public sealed record PlaceholderUnresolvedError : BaseError
{
    public const string ErrorCode = "NOTIFICATION_PLACEHOLDER_UNRESOLVED";

    /// <summary>Clave de la extensión ProblemDetails que lleva el marcador sin resolver.</summary>
    private const string PlaceholderExtension = "placeholder";

    /// <summary>Clave del marcador que quedó sin valor.</summary>
    public string Placeholder { get; init; }

    /// <summary>Persona para la que faltaba el valor. Puede faltar para una sola de varias.</summary>
    public string Recipient { get; init; }

    public PlaceholderUnresolvedError(string placeholder, string recipient)
        : base(
            $"El marcador '{{{{{placeholder}}}}}' no tiene valor para {recipient}.",
            ErrorCode,
            ErrorKind.Invalid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placeholder);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

        Placeholder = placeholder;
        Recipient = recipient;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PlaceholderExtension] = Placeholder
        };
}
