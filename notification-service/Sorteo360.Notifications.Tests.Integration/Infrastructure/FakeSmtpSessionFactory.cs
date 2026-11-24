using System.Collections.Concurrent;
using Sorteo360.Notifications.Core.Sending;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// Enchufa <see cref="FakeSmtpTransport"/> en el worker. El doble sigue siendo el de siempre —es
/// él quien acepta o falla con el código que se le pida— y esto solo lo pone donde el worker lo
/// busca, guardando además el mensaje tal como el worker lo armó.
/// <para>
/// Lo que este doble <b>no</b> prueba son los bytes: para eso está el único test de punta a punta
/// contra Mailpit. Acá se prueban las reglas, que es lo que ningún servidor real puede dar, porque
/// Mailpit acepta todo lo que se le manda y nunca contesta un 535.
/// </para>
/// </summary>
public sealed class FakeSmtpSessionFactory : ISmtpSessionFactory
{
    private readonly ConcurrentQueue<OutboundEmail> _sent = new();

    /// <summary>El doble que decide si cada entrega pasa o falla.</summary>
    public FakeSmtpTransport Transport { get; } = new();

    /// <summary>Los mensajes tal como los armó el worker, en el orden en que salieron.</summary>
    public IReadOnlyList<OutboundEmail> Sent => [.. _sent];

    /// <summary>Cuántas veces se abrió una conexión. Reutilizar una es lo normal.</summary>
    public int OpenedSessions => _opened;

    /// <summary>
    /// Cuánto tarda cada entrega. Sirve para que dos drenadores se solapen de verdad en el test de
    /// concurrencia: con entregas instantáneas la carrera no llega a ocurrir y el test pasaría sin
    /// haber ejercitado el <c>SKIP LOCKED</c>.
    /// </summary>
    public TimeSpan SendDelay { get; set; } = TimeSpan.Zero;

    private int _opened;
    private Exception? _openFailure;

    /// <inheritdoc />
    public Task<ISmtpSession> OpenAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _opened);

        // Un fallo al abrir que no es un código SMTP: el worker no lo puede clasificar con la
        // tabla de reintentos y tiene que decidir igual qué hacer con la fila.
        if (_openFailure is not null)
            throw _openFailure;

        return Task.FromResult<ISmtpSession>(new Session(this));
    }

    /// <summary>Abrir la conexión falla con una excepción que el worker no sabe leer.</summary>
    public void FailToOpenWith(Exception failure) => _openFailure = failure;

    /// <summary>Vuelve a aceptar todo y olvida lo entregado.</summary>
    public void Reset()
    {
        Transport.Reset();
        _sent.Clear();
        _openFailure = null;
        Interlocked.Exchange(ref _opened, 0);
    }

    private sealed class Session : ISmtpSession
    {
        private readonly FakeSmtpSessionFactory _owner;

        public Session(FakeSmtpSessionFactory owner) => _owner = owner;

        public async Task<string> SendAsync(OutboundEmail email, CancellationToken cancellationToken)
        {
            if (_owner.SendDelay > TimeSpan.Zero)
                await Task.Delay(_owner.SendDelay, cancellationToken);

            try
            {
                var response = _owner.Transport.Send(Message(email));
                _owner._sent.Enqueue(email);

                return response;
            }
            catch (FakeSmtpException ex)
            {
                // El doble habla en códigos SMTP; el worker espera la excepción del transporte.
                // Clasificar el código sigue siendo del worker.
                throw new SmtpDeliveryException(ex.StatusCode, ex.Response, ex);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Lo que el doble guarda. Las cabeceras van acá tal como las decidió el worker; cómo se
        /// escriben en el sobre es del transporte de verdad y lo comprueba Mailpit.
        /// </summary>
        private static FakeSmtpMessage Message(OutboundEmail email)
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OutboundHeaders.SmtpApi] = OutboundHeaders.UniqueArgs(email.RecipientId)
            };

            if (email.UnsubscribeUrl is not null)
                headers[OutboundHeaders.ListUnsubscribe] = OutboundHeaders.UnsubscribeValue(email.UnsubscribeUrl);

            return new FakeSmtpMessage(
                FromAddress: email.FromAddress,
                FromName: email.FromName,
                ToAddress: email.ToAddress,
                Subject: email.Subject,
                HtmlBody: email.HtmlBody,
                TextBody: email.TextBody,
                Headers: headers);
        }

    }
}
