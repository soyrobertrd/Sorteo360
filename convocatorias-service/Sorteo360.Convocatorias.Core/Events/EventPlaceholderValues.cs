using System.Globalization;
using Sorteo360.Convocatorias.Core.Rendering;

namespace Sorteo360.Convocatorias.Core.Events;

/// <summary>
/// De dónde sale el valor de cada marcador de grano de evento: <c>{{evento}}</c>,
/// <c>{{lugar}}</c> y <c>{{fecha}}</c> (spec <c>02</c> §2).
/// <para>
/// <b>Un dato que falta sale como cadena vacía, nunca como un texto plausible.</b> Es la mitad
/// que hace posible el rechazo del lanzamiento: lo que decide que un valor falta es que venga
/// vacío, así que un "por confirmar" de relleno acá se leería como un evento completo y el
/// correo saldría con el hueco que la regla existe para impedir (spec <c>02</c> §2.2).
/// </para>
/// <para>
/// Lo pregunta el lanzamiento antes del <c>202</c>. El worker de la spec <c>04</c> §1 usa el
/// mismo mapa como <c>sharedData</c> de la llamada a notification-service, que es por qué esto
/// vive en Core y no dentro del servicio de lanzamiento.
/// </para>
/// </summary>
public static class EventPlaceholderValues
{
    /// <summary>
    /// Entre dos ciudades de un mismo recorrido. Punto y coma y no coma: la coma ya separa la
    /// ciudad de su país, y "Madrid, España, Barcelona, España" no se lee.
    /// </summary>
    private const string CitySeparator = "; ";

    /// <summary>
    /// El idioma en que se escribe la fecha. Fijo y no el del proceso: sin esto, un contenedor
    /// arrancado en inglés mandaría "12 de June de 2026". Que sea español y no el idioma del
    /// evento es lo que hay hoy —la copia sembrada es solo <c>es</c>— y se mueve cuando llegue
    /// la elección de plantilla por idioma.
    /// </summary>
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Día, mes en letra y año: "12 de junio de 2026".</summary>
    private const string LongDateFormat = "d 'de' MMMM 'de' yyyy";

    /// <summary>
    /// Los tres valores de grano de evento, por clave. Siempre las tres claves: una ausente y
    /// una vacía significan lo mismo para quien comprueba, y tener siempre las tres deja al
    /// indexador ser un indexador.
    /// </summary>
    /// <param name="summary">El evento, tal como lo devolvió events-service.</param>
    public static IReadOnlyDictionary<string, string> From(EventSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SupportedPlaceholder.EventName]     = Trimmed(summary.Name),
            [SupportedPlaceholder.EventLocation] = Location(summary.Cities),
            [SupportedPlaceholder.EventDates]    = Dates(summary.StartDate, summary.EndDate),
        };
    }

    /// <summary>
    /// Las ciudades del recorrido, nombre y país, en el orden en que se recorren (spec <c>01</c>
    /// §4.1). Una ciudad sin orden va al final y por nombre, que es lo único estable que queda
    /// cuando el dato que ordena no está.
    /// </summary>
    private static string Location(IReadOnlyList<EventCity> cities)
    {
        if (cities.Count == 0)
            return string.Empty;

        var parts = cities
            .Where(city => !string.IsNullOrWhiteSpace(city.Name))
            .OrderBy(city => city.DisplayOrder ?? int.MaxValue)
            .ThenBy(city => city.Name, StringComparer.Ordinal)
            .Select(city => string.IsNullOrWhiteSpace(city.Country)
                ? city.Name.Trim()
                : $"{city.Name.Trim()}, {city.Country.Trim()}");

        return string.Join(CitySeparator, parts);
    }

    /// <summary>
    /// El día, o el tramo cuando el evento dura más de uno. Un evento de un solo día escrito
    /// como tramo —"del 12 al 12"— se lee como un error de quien lo creó.
    /// <para>
    /// <b>Una fecha sin poner sale vacía.</b> <c>StartDate</c> y <c>EndDate</c> no son anulables,
    /// así que un evento que llegue sin ellas trae <c>0001-01-01</c>, y escribirlo tal cual
    /// mandaría "se celebrará el 1 de enero de 0001" a mil personas: exactamente el texto
    /// plausible que esta clase existe para no producir.
    /// </para>
    /// </summary>
    private static string Dates(DateOnly start, DateOnly end)
    {
        if (start == default)
            return string.Empty;

        return start == end
            ? start.ToString(LongDateFormat, Spanish)
            : $"del {start.ToString(LongDateFormat, Spanish)} al {end.ToString(LongDateFormat, Spanish)}";
    }

    /// <summary>
    /// El texto sin espacios alrededor, y vacío cuando no hay texto. Un nombre que solo trae
    /// espacios es un nombre que falta, y tiene que salir de acá como tal.
    /// </summary>
    private static string Trimmed(string? value) => value?.Trim() ?? string.Empty;
}
