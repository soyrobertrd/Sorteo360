using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Errors;

/// <summary>
/// La plantilla nombra marcadores de grano de evento —<c>{{evento}}</c>, <c>{{lugar}}</c>,
/// <c>{{fecha}}</c>— para los que el evento no tiene valor. Rechaza el lanzamiento nombrando
/// cada campo, para que nadie mande "se celebrará en ." a mil personas.
/// <para>
/// Se comprueba <b>antes</b> del <c>202</c>: después ya no queda a quién avisar. Es la mitad de
/// evento de la regla de grano; la de persona no rechaza nada, <c>{{nombre}}</c> se cae a un
/// saludo neutro (spec <c>02</c> §2.2).
/// </para>
/// </summary>
public sealed record EventValuesMissingError : BaseError
{
    public const string ErrorCode = "TEMPLATE_EVENT_VALUES_MISSING";

    /// <summary>Clave de la extensión ProblemDetails que lleva los campos sin valor.</summary>
    private const string FieldsExtension = "fields";

    /// <summary>Los marcadores de evento que quedaron sin valor, en el orden en que aparecen.</summary>
    public IReadOnlyList<string> Fields { get; init; }

    public EventValuesMissingError(IReadOnlyList<string> fields)
        : base(
            $"El evento no tiene valor para: {string.Join(", ", fields ?? [])}.",
            ErrorCode,
            ErrorKind.Invalid)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Count == 0)
            throw new ArgumentException("Un fallo por valores de evento ausentes tiene que nombrar al menos uno.", nameof(fields));

        Fields = fields;
    }

    public override IReadOnlyDictionary<string, object?> ProblemExtensions =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [FieldsExtension] = Fields
        };
}
