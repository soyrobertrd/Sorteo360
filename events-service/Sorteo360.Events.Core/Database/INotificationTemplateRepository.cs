using Sorteo360.Events.Core.Templates;

namespace Sorteo360.Events.Core.Database;

/// <summary>
/// Lectura de la tabla de plantillas. <b>No hay escritura en fase 1</b>: el conjunto lo siembra una
/// migración de datos y nadie edita una plantilla desde la API.
///
/// <para>
/// Que la tabla exista hoy es lo que hace aditivos los endpoints de administración del día que el
/// cliente gestione sus propias plantillas. Es una inferencia honesta y no un requisito: el cliente
/// lo declaró sobre las plantillas <i>de campaña</i>, y extenderlo a las transaccionales es
/// decisión nuestra —la forma de la tabla hace que salga gratis en cualquiera de los dos casos—.
/// </para>
/// </summary>
public interface INotificationTemplateRepository
{
    /// <summary>
    /// La plantilla de esa clave en ese idioma, o <c>null</c> si no está sembrada.
    ///
    /// <para>
    /// <b>No devuelve <c>Result</c>, y no lanza.</b> Su único llamador es el notificador, que no
    /// tiene canal de error por diseño: una plantilla que falta es un aviso que no sale, nunca un
    /// sometimiento que se cae. Quien llama lo registra como
    /// <see cref="Raffles.QuoteNotificationStatus.Failed"/>.
    /// </para>
    /// </summary>
    Task<NotificationTemplate?> FindAsync(string templateKey, string language, CancellationToken cancellationToken);
}
