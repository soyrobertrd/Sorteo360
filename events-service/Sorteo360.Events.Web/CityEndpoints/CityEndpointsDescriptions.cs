namespace Sorteo360.Events.Web.CityEndpoints;

/// <summary>
/// Textos en español que documentan los endpoints de ciudades en OpenAPI (los renderiza
/// /scalar). Ver <c>EventEndpointsDescriptions</c> para la convención completa.
/// </summary>
internal static class CityEndpointsDescriptions
{
    public const string UpdateCitySummary =
        "Reemplaza los datos editables de la ciudad del evento.";

    public const string UpdateCityDescription =
        """
        Es un reemplazo completo de los campos mutables, no un ajuste parcial: lo que no
        se envía queda en su valor por defecto. Por eso `timeZone` es obligatorio acá:
        omitirlo no conserva la zona guardada. Un identificador que no es IANA se rechaza
        con `TIME_ZONE_UNKNOWN`. La zona se puede corregir aunque el sorteo del evento ya
        esté completado, siempre que el evento siga abierto: la próxima reemisión del acta
        escribe las horas en la zona nueva.

        Vale para un evento de cualquier tipo en estado no terminal. Un evento tiene
        exactamente una ciudad, la que recibió al crearse: esta ruta la corrige, y
        ninguna agrega ni quita otra. Devuelve la ciudad actualizada.
        """;
}
