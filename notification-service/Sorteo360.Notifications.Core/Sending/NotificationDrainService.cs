using Microsoft.Extensions.Logging;
using Sorteo360.Notifications.Core.Rendering;

namespace Sorteo360.Notifications.Core.Sending;

/// <inheritdoc cref="INotificationDrainService" />
public sealed class NotificationDrainService : INotificationDrainService
{
    private readonly ISendQueue _queue;
    private readonly ISuppressionList _suppressionList;
    private readonly ISubstitutionEngine _substitutionEngine;
    private readonly ISmtpSessionFactory _sessionFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationDrainService> _logger;

    public NotificationDrainService(
        ISendQueue queue,
        ISuppressionList suppressionList,
        ISubstitutionEngine substitutionEngine,
        ISmtpSessionFactory sessionFactory,
        TimeProvider timeProvider,
        ILogger<NotificationDrainService> logger)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(suppressionList);
        ArgumentNullException.ThrowIfNull(substitutionEngine);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _queue = queue;
        _suppressionList = suppressionList;
        _substitutionEngine = substitutionEngine;
        _sessionFactory = sessionFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> DrainAsync(CancellationToken cancellationToken)
    {
        // La sesión se abre con el primer mensaje que de verdad va a salir, y no antes. Ni la
        // vuelta que no encuentra nada pendiente ni una fila suprimida llegan a abrir un socket:
        // la supresión se responde sin conexión, que es lo que pide la especificación.
        ISmtpSession? session = null;
        var drained = 0;

        async Task<ISmtpSession> OpenSessionAsync(CancellationToken token) =>
            session ??= await _sessionFactory.OpenAsync(token);

        try
        {
            // Se drena hasta vaciar. No hay tope por vuelta: el apagado ordenado ya sale por el
            // token, y un límite de caudal es justo lo que la especificación no quiere.
            while (!cancellationToken.IsCancellationRequested)
            {
                var claimed = await _queue.TryDrainNextAsync(
                    (pending, token) => AttemptAsync(OpenSessionAsync, pending, token),
                    cancellationToken);

                if (claimed.IsFailure)
                {
                    // La cola no se pudo leer o escribir. Se corta la vuelta: insistir contra una
                    // base que no responde no cambia nada y el worker ya espera más tras un fallo.
                    _logger.LogError(
                        "No se pudo drenar la cola de envíos ({Code}): {Message}",
                        claimed.Error.Code,
                        claimed.Error.Message);

                    break;
                }

                if (!claimed.Value)
                    break;

                drained++;
            }
        }
        finally
        {
            if (session is not null)
                await session.DisposeAsync();
        }

        return drained;
    }

    /// <summary>
    /// Un intento sobre una fila: la hora y la supresión <b>antes</b> de que exista socket, luego
    /// la sustitución, la conexión, la transacción, y la clasificación de lo que haya fallado.
    /// <para>
    /// La conexión se pide con <paramref name="openSession"/> y no se recibe abierta, porque una
    /// fila suprimida o vencida se resuelve sin hablar con ningún servidor.
    /// </para>
    /// </summary>
    private async Task<SendOutcome> AttemptAsync(
        Func<CancellationToken, Task<ISmtpSession>> openSession,
        PendingSend pending,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        // La fila esperó más de la hora sin llegar a salir. No se intenta: correo rancio es peor
        // que correo ausente.
        if (SendRetryPolicy.HasGivenUp(pending.QueuedAt, now))
        {
            _logger.LogWarning(
                "Se abandona el envío {RecipientId} del lote {BatchId}: lleva más de una hora en cola",
                pending.RecipientId,
                pending.BatchId);

            return SendOutcome.Failed(SendRetryPolicy.GaveUpReason);
        }

        var suppression = await _suppressionList.ReasonForAsync(pending.Email, cancellationToken);

        if (suppression is not null)
        {
            _logger.LogInformation(
                "No se manda el envío {RecipientId}: la dirección está suprimida ({Reason})",
                pending.RecipientId,
                suppression);

            return SendOutcome.Dropped(suppression);
        }

        var rendered = _substitutionEngine.Render(pending.Subject, pending.Body, pending.Values);

        var email = new OutboundEmail(
            RecipientId: pending.RecipientId,
            FromAddress: pending.FromAddress,
            FromName: pending.FromName,
            ToAddress: pending.Email,
            ToName: pending.Name,
            Subject: rendered.Subject,
            HtmlBody: rendered.HtmlBody,
            TextBody: rendered.TextBody,
            UnsubscribeUrl: UnsubscribeLink.From(pending.Values));

        try
        {
            // Recién acá se abre el socket: hay un mensaje que mandar. Conectar y autenticar son
            // parte del envío, así que si fallan caen en la misma clasificación que el resto.
            var session = await openSession(cancellationToken);
            var queuedAs = await session.SendAsync(email, cancellationToken);

            _logger.LogInformation(
                "Envío {RecipientId} del lote {BatchId} aceptado por el servidor: {Response}",
                pending.RecipientId,
                pending.BatchId,
                queuedAs);

            return SendOutcome.Sent(queuedAs);
        }
        catch (SmtpDeliveryException ex)
        {
            return Classify(ex.StatusCode, ex.Message, ex, pending, now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un fallo que no viene del servidor SMTP es un error nuestro, y aun así la fila tiene
            // que avanzar: dejarla intacta la devolvería a la cabeza de la cola en cada vuelta,
            // tapando a todas las demás durante una hora entera. Se trata como transitorio, que le
            // gasta un intento y la manda al final con su espera.
            _logger.LogError(
                ex,
                "Fallo inesperado mandando el envío {RecipientId} del lote {BatchId}",
                pending.RecipientId,
                pending.BatchId);

            return Classify(statusCode: null, ex.Message, ex, pending, now);
        }
    }

    /// <summary>
    /// Traduce el fallo en lo que hay que escribir. La regla es de SMTP: <c>4xx</c> se reintenta,
    /// <c>5xx</c> no. El <c>535</c> además se registra como error, porque no es el buzón de quien
    /// recibe, es nuestra API key, y ningún reintento lo arregla.
    /// </summary>
    private SendOutcome Classify(
        int? statusCode,
        string error,
        Exception failure,
        PendingSend pending,
        DateTimeOffset now)
    {
        switch (SmtpFailureClassifier.Classify(statusCode))
        {
            case SmtpFailureKind.AuthenticationFailure:
                _logger.LogError(
                    failure,
                    "El servidor SMTP rechazó nuestras credenciales al mandar el envío {RecipientId}. " +
                    "No es el buzón de quien recibe: ningún reintento lo arregla",
                    pending.RecipientId);

                return SendOutcome.Failed(error);

            case SmtpFailureKind.Terminal:
                _logger.LogWarning(
                    failure,
                    "El servidor rechazó el envío {RecipientId} de forma definitiva; no se reintenta",
                    pending.RecipientId);

                return SendOutcome.Failed(error);
        }

        var nextAttemptAt = SendRetryPolicy.NextAttemptAt(pending.QueuedAt, now, pending.AttemptCount + 1);

        if (nextAttemptAt is null)
        {
            _logger.LogWarning(
                failure,
                "Se abandona el envío {RecipientId}: el siguiente reintento caería fuera de la hora",
                pending.RecipientId);

            return SendOutcome.Failed(SendRetryPolicy.GaveUpReason);
        }

        _logger.LogWarning(
            failure,
            "Fallo transitorio en el envío {RecipientId}; se reintenta a las {NextAttemptAt:O}",
            pending.RecipientId,
            nextAttemptAt.Value);

        return SendOutcome.Retry(nextAttemptAt.Value, error);
    }
}
