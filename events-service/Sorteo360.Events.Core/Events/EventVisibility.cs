namespace Sorteo360.Events.Core.Events;

/// <summary>
/// Quién puede ver el evento. Es del evento y no de la campaña: gobierna más que las
/// convocatorias, y un indicador por campaña dejaría que un mismo evento llevara dos campañas
/// que se contradicen.
/// <para>
/// Conjunto cerrado de cadenas, como <see cref="EventType"/>, <see cref="EventStatus"/> y
/// <see cref="AllowedLanguage"/>, y no un booleano <c>IsPublic</c>: el booleano rompe el patrón
/// que ya siguen los otros tres y no admite el tercer valor que tarde o temprano aparece, el
/// evento que no se lista pero se alcanza por enlace.
/// </para>
/// </summary>
public static class EventVisibility
{
    public const string Public  = "public";
    public const string Private = "private";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Public, Private
    };
}
