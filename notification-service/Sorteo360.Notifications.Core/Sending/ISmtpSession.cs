namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// Una conexión SMTP abierta, por la que pasan los mensajes de una vuelta del worker uno detrás de
/// otro. Reutilizar la conexión no es una optimización: mil mensajes son mil transacciones
/// secuenciales sobre la misma sesión, y el cliente serializa igual.
/// </summary>
public interface ISmtpSession : IAsyncDisposable
{
    /// <summary>
    /// Entrega un mensaje. Devuelve la respuesta del servidor —el <c>Ok: queued as &lt;token&gt;</c>
    /// con el que aceptó— y lanza <see cref="SmtpDeliveryException"/> cuando la transacción murió.
    /// </summary>
    Task<string> SendAsync(OutboundEmail email, CancellationToken cancellationToken);
}

/// <summary>
/// Abre la conexión. Es un seam de composición, no un adaptador con doble implementación en
/// producción: <b>hay un solo transporte</b> y corre igual contra Mailpit que contra el proveedor.
/// Un adaptador de mentira detrás de esto dejaría la conexión, el STARTTLS, el AUTH y el armado
/// MIME ejecutándose por primera vez en producción, en la única corrida que nadie puede mirar.
/// </summary>
public interface ISmtpSessionFactory
{
    /// <summary>Conecta y autentica. Lo que falle acá es un fallo de envío como cualquier otro.</summary>
    Task<ISmtpSession> OpenAsync(CancellationToken cancellationToken);
}
