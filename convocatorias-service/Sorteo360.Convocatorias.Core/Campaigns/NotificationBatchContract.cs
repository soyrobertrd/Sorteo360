using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Las dos reglas que notification-service cobra sobre un lote antes de mirar nada más (spec
/// <c>04</c> §2 y §2.2): el tope de mil y la clave que no puede estar en las dos bolsas.
/// <para>
/// <b>Viven acá y las corren los dos transportes.</b> Son reglas de quien recibe, así que lo que
/// hace de quien recibe es lo que las cobra: el stub y el cliente tipado llaman a
/// <see cref="Validate"/> antes de hacer nada. Ponerlas solo en el cliente dejaría al stub
/// aceptando un lote que el servicio real rechaza, y stage dejaría de ser un ensayo de producción,
/// que es lo único que la spec <c>04</c> §4 le pide.
/// </para>
/// <para>
/// <b>Llegar a cualquiera de las dos es un fallo de este lado.</b> Trocear es trabajo de quien
/// llama (§2.2) y las dos bolsas las arma este servicio, así que ninguna de las dos puede
/// dispararla un dato de fuera. Por eso son un error con código y no una excepción: el lote entero
/// se cae, sus filas se quedan <c>pending</c> y quedan en el log con el nombre de lo que pasó.
/// </para>
/// </summary>
public static class NotificationBatchContract
{
    /// <summary>
    /// El tope por llamada (spec <c>04</c> §2.2). <b>No es configuración</b>: es lo que declara el
    /// contrato, y por encima de él la petición entera es un 400.
    /// <para>
    /// Es también el tamaño con el que se trocea, y a propósito el mismo número: el que parte y el
    /// que rechaza no pueden ser dos constantes, o el día que una se mueva el emisor empieza a
    /// armar lotes que el receptor tira.
    /// </para>
    /// </summary>
    public const int MaxRecipients = 1000;

    /// <summary>
    /// Revisa el lote contra las dos reglas. Éxito es que se puede mandar tal cual.
    /// </summary>
    public static UnitResult<BaseError> Validate(SendBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // El tope de arriba y nada más. El contrato dice "de 1 a 1000" y el borde de abajo no se
        // cobra acá a propósito: `Chunk` nunca rinde un lote vacío y la entrega ni siquiera lee la
        // plantilla si no hay a quién escribirle, así que un tercer error sería una rama que no
        // corre. El receptor lo cobrará, que es donde el dato podría venir de fuera.
        if (request.Recipients.Count > MaxRecipients)
        {
            return UnitResult.Failure<BaseError>(
                new NotificationBatchTooLargeError(request.Recipients.Count, MaxRecipients));
        }

        foreach (var recipient in request.Recipients)
        {
            foreach (var key in recipient.Data.Keys)
            {
                // Nunca una regla de precedencia (§2): no hay motivo legítimo para mandar una
                // clave dos veces, y un ganador silencioso es un generador de fallos.
                if (request.SharedData.ContainsKey(key))
                    return UnitResult.Failure<BaseError>(new NotificationDataKeyConflictError(key));
            }
        }

        return UnitResult.Success<BaseError>();
    }
}
