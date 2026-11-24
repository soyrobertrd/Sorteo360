namespace Sorteo360.Events.Core.Templates;

/// <summary>
/// El enlace profundo que lleva un correo hasta el evento del que habla. <b>Un solo lugar</b>, para
/// que cambiar la ruta sea una línea y no una búsqueda por cuatro plantillas.
///
/// <para>
/// <b>Apunta al evento y no a la cotización, a propósito.</b> No existe una ruta por categoría: las
/// cotizaciones son un área dentro de la página del evento, así que el enlace deja a quien lo abre
/// en el evento y la categoría la encuentra ahí. Es una mirada de más, no un callejón sin salida, y
/// es exactamente por eso que este ayudante recibe el id del <i>evento</i> y no el de la cotización.
/// </para>
///
/// <para>
/// <c>/eventos/{id}</c> es la ruta del frontend real —el de Robert—, confirmada por el dueño el
/// 2026-09-13. Que el <c>frontend/</c> de este repositorio coincida es una casualidad y no fue la
/// evidencia.
/// </para>
/// </summary>
public static class EventLink
{
    /// <summary>
    /// <c>{baseUrl}/eventos/{eventId}</c>.
    /// <para>
    /// La barra sobrante de la base se recorta para que <c>https://app.sorteo360.local</c> y
    /// <c>https://app.sorteo360.local/</c> produzcan el mismo enlace: las dos formas son razonables en
    /// una variable de entorno, y una barra doble en un correo se ve como un error de la
    /// plataforma aunque el navegador la tolere.
    /// </para>
    /// </summary>
    public static string For(string baseUrl, Guid eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        return $"{baseUrl.TrimEnd('/')}/eventos/{eventId}";
    }
}
