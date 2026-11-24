using CSharpFunctionalExtensions;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// La cola de envíos pendientes, vista desde el worker.
/// </summary>
public interface ISendQueue
{
    /// <summary>
    /// Toma la siguiente fila que toca, se la pasa a <paramref name="send"/>, escribe lo que
    /// devuelva y confirma. Devuelve <c>false</c> cuando no queda nada por hacer.
    /// <para>
    /// La fila se toma con <c>FOR UPDATE SKIP LOCKED</c>, así que dos drenadores contra la misma
    /// cola mandan cada fila exactamente una vez: el segundo no espera al primero, se salta la
    /// fila que el primero tiene tomada. Compose corre un contenedor por servicio y hoy hay un
    /// solo drenador, pero una segunda réplica duplicaría todos los envíos pendientes, y la
    /// cláusula cuesta una línea ahora contra una reescritura después.
    /// </para>
    /// <para>
    /// Una fila, una transacción. El estado se escribe en cuanto termina <b>su</b> transacción
    /// SMTP y no al final del lote: un lote puede morir a la mitad, y una sola escritura al final
    /// perdería las filas que ya salieron.
    /// </para>
    /// </summary>
    /// <para>
    /// Un fallo de base de datos vuelve como error y no como excepción: el worker decide si corta
    /// la vuelta, y ninguna excepción de EF llega a cruzar esta frontera.
    /// </para>
    /// </summary>
    /// <param name="send">Qué hacer con la fila tomada.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> si se resolvió una fila; <c>false</c> si no quedaba nada que hacer.</returns>
    Task<Result<bool, BaseError>> TryDrainNextAsync(
        Func<PendingSend, CancellationToken, Task<SendOutcome>> send,
        CancellationToken cancellationToken);
}
