using System.Collections.Concurrent;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// La pata SMTP en proceso, programable: acepta mensajes, los guarda y falla cuando se le
/// pide, con el código de respuesta que se le pida.
/// <para>
/// Un doble acá no es una concesión, es la única forma de producir un <c>535</c> a voluntad:
/// Mailpit acepta todo lo que se le mande, así que la tabla de reintentos del worker
/// (4xx reintenta, 5xx nunca, 535 es terminal y ruidoso) no se puede ejercitar contra un
/// servidor real. Los bytes los prueba el único test de punta a punta contra Mailpit, que es
/// lo que ningún doble puede probar. Precedente: <c>CrmDirectoryStub</c> y <c>FileStoreStub</c>
/// en events-service.
/// </para>
/// <para>
/// Hoy no hay worker que lo use: llega con la especificación 03. Mientras tanto se ejercita
/// directamente en su smoke test, igual que hicieron los dobles de convocatorias antes de
/// tener cliente — el día que exista el worker, un fallo señala al worker y no al doble.
/// </para>
/// </summary>
public sealed class FakeSmtpTransport
{
    private readonly ConcurrentQueue<FakeSmtpMessage> _sent = new();
    private readonly Dictionary<string, FakeSmtpFailure> _byAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private FakeSmtpFailure? _programmedFailure;
    private int _remainingFailures;

    /// <summary>Respuesta con la que un servidor SMTP acepta un mensaje.</summary>
    public const int AcceptedStatusCode = 250;

    /// <summary>Los mensajes que pasaron, en el orden en que pasaron.</summary>
    public IReadOnlyList<FakeSmtpMessage> Sent => [.. _sent];

    /// <summary>Cuántas transacciones se intentaron, hayan pasado o no.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// Entrega un mensaje. Devuelve el <c>250 Ok: queued as &lt;token&gt;</c> que el worker
    /// guarda en <c>smtp_queued_as</c>, o lanza <see cref="FakeSmtpException"/> con el
    /// código programado. Clasificar ese código en transitorio o terminal es del worker,
    /// no de este doble: un doble que ya supiera la respuesta no probaría nada.
    /// </summary>
    public string Send(FakeSmtpMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_gate)
        {
            AttemptCount++;

            // Primero lo dirigido a una dirección: un lote no sale en un orden garantizado, así
            // que "esta persona falla" es lo único que un test puede fijar. Programar "la próxima
            // falla" y dar por hecho a quién le toca es una prueba que pasa la mitad de las veces.
            if (_byAddress.TryGetValue(message.ToAddress, out var addressed))
                throw new FakeSmtpException(addressed.StatusCode, addressed.Response);

            if (_programmedFailure is { } failure && _remainingFailures != 0)
            {
                if (_remainingFailures > 0)
                    _remainingFailures--;

                throw new FakeSmtpException(failure.StatusCode, failure.Response);
            }

            _sent.Enqueue(message);
            return $"{AcceptedStatusCode} Ok: queued as {Guid.NewGuid():N}";
        }
    }

    /// <summary>Las próximas <paramref name="times"/> entregas fallan con ese código.</summary>
    public void FailNextWith(int statusCode, string response, int times = 1)
    {
        lock (_gate)
        {
            _programmedFailure = new FakeSmtpFailure(statusCode, response);
            _remainingFailures = times;
        }
    }

    /// <summary>Toda entrega a esa dirección falla con ese código, salga cuando salga.</summary>
    public void FailForAddressWith(string email, int statusCode, string response)
    {
        lock (_gate)
        {
            _byAddress[email] = new FakeSmtpFailure(statusCode, response);
        }
    }

    /// <summary>Toda entrega falla con ese código hasta un <see cref="Reset"/>.</summary>
    public void FailAlwaysWith(int statusCode, string response)
    {
        lock (_gate)
        {
            _programmedFailure = new FakeSmtpFailure(statusCode, response);
            _remainingFailures = -1;
        }
    }

    /// <summary>Vuelve a aceptar todo y olvida lo entregado.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _programmedFailure = null;
            _remainingFailures = 0;
            _byAddress.Clear();
            AttemptCount = 0;
            _sent.Clear();
        }
    }

    private sealed record FakeSmtpFailure(int StatusCode, string Response);
}

/// <summary>
/// Un mensaje tal como sale por el socket: uno por destinatario, sin <c>Cc</c> ni <c>Bcc</c>
/// —sobre SMTP eso es el comportamiento nativo, no un caso especial— y con las dos partes
/// MIME. Las cabeceras sueltas (<c>X-SMTPAPI</c>, <c>List-Unsubscribe</c>) viajan aparte.
/// </summary>
public sealed record FakeSmtpMessage(
    string FromAddress,
    string? FromName,
    string ToAddress,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>Un servidor SMTP que rechazó la transacción, con su código de respuesta.</summary>
public sealed class FakeSmtpException : Exception
{
    public FakeSmtpException(int statusCode, string response)
        : base($"{statusCode} {response}")
    {
        StatusCode = statusCode;
        Response = response;
    }

    /// <summary>El código de respuesta SMTP. <c>4xx</c> es transitorio, <c>5xx</c> terminal.</summary>
    public int StatusCode { get; }

    /// <summary>El texto que acompañó al código.</summary>
    public string Response { get; }
}
