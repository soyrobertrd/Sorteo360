namespace Sorteo360.Platform.Errors;

public record BaseError
{
    private static readonly IReadOnlyDictionary<string, object?> NoExtensions =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    public string Message { get; init; }
    public string? Code { get; init; }
    public ErrorKind Kind { get; init; }

    public BaseError(string message, string? code = null, ErrorKind kind = ErrorKind.Internal)
    {
        ArgumentNullException.ThrowIfNull(message);
        Message = message;
        Code = code;
        Kind = kind;
    }

    /// <summary>
    /// Datos que este error añade a la respuesta ProblemDetails, además del <see cref="Code"/>.
    /// Vacío salvo que una subclase lo sobrescriba, y por eso la mayoría de los errores responden
    /// igual que antes de que existiera esta propiedad.
    /// <para>
    /// Es lo que permite a un fallo llevar el dato con el que quien consume lo resuelve, sin que el
    /// endpoint arme la respuesta a mano y sin que el chasis conozca el tipo del servicio. La clave
    /// <c>code</c> se descarta: ese identificador sale de <see cref="Code"/> y de ningún otro sitio.
    /// </para>
    /// </summary>
    public virtual IReadOnlyDictionary<string, object?> ProblemExtensions => NoExtensions;
}
