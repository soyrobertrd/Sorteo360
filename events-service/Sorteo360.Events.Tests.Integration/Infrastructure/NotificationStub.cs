using System.Text.Json;
using Shouldly;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// notification-service visto desde events-service, sustituido por un servidor real que habla HTTP.
/// <para>
/// Se sustituye el vecino y no <c>INotificationClient</c> a propósito, por la misma razón que ya
/// explica <c>CrmDirectoryStub</c>: lo que hay que ejercitar es la cadena entera —el cliente
/// tipado, la identidad de servicio del chasis y su tubería de resiliencia—, y nada de eso corre si
/// se reemplaza la interfaz por un doble. Es también el patrón <c>NeighbourStub</c> que
/// convocatorias ya estableció.
/// </para>
/// <para>
/// <b>Lo que reporta es lo del test que corre, y nada más.</b> WireMock apunta una petición en su
/// registro cuando termina de contestarla, no cuando la recibe, así que la que el vecino lento
/// contesta tarde aterriza cuando el test que la mandó ya acabó y el siguiente ya limpió el
/// registro —era la fuga de PGTI-32, y el test que la reproduce es <c>NotificationStubTests</c>—.
/// Por eso lo que se mira no es "lo que hay en el registro" sino "lo que llegó después del último
/// corte": la hora de llegada sí es del momento en que la petición entró, y ningún envío de este
/// test puede haber entrado antes de que este test empezara.
/// </para>
/// <para>
/// Eso último se apoya en que el aviso se espera dentro de la petición que lo provoca, como hace
/// hoy <c>QuoteService</c>: uno que saliera por su cuenta —una cola, un proceso de fondo— podría
/// entrar <b>después</b> del corte del test siguiente, y entonces el corte no alcanzaría. El día
/// que eso se construya, esto hay que volver a pensarlo.
/// </para>
/// </summary>
public sealed class NotificationStub : IDisposable
{
    private const string NotificationsPath = "/v1/notifications";

    /// <summary>Retraso que el chasis ya no espera: su tiempo por intento es de 10 s.</summary>
    private static readonly TimeSpan PastTheChassisAttemptTimeout = TimeSpan.FromSeconds(15);

    private readonly WireMockServer _server = WireMockServer.Start();

    /// <summary>
    /// El corte: desde cuándo cuenta lo que llegue. Lo mueve cada limpieza del registro, y con él
    /// deja fuera lo que entró antes aunque se apunte después.
    /// </summary>
    private DateTime _countingFrom = DateTime.UtcNow;

    public NotificationStub() => ServeAccepted();

    /// <summary>URL base, tal como la recibe <c>Services__Notifications__BaseUrl</c>.</summary>
    public string Url => _server.Url!;

    /// <summary>Todo POST de aviso que el servidor lleva apuntado, venga del test que venga.</summary>
    private IEnumerable<ILogEntry> LoggedSends => _server.LogEntries.Where(entry =>
        entry.RequestMessage?.Path == NotificationsPath
        && string.Equals(entry.RequestMessage?.Method, "POST", StringComparison.OrdinalIgnoreCase));

    /// <summary>Los que llegaron después del último corte, que son los de este test.</summary>
    private IEnumerable<ILogEntry> SendsSinceReset =>
        LoggedSends.Where(entry => entry.RequestMessage!.DateTime >= _countingFrom);

    /// <summary>
    /// Peticiones POST recibidas. <b>No es el número de avisos</b>: el chasis reintenta, así que un
    /// mismo aviso puede aparecer dos veces. Para contar avisos, <see cref="SendKeys"/>. Sirve para
    /// la afirmación que sí es exacta: que no llegó ninguna.
    /// </summary>
    private int RequestCount => SendsSinceReset.Count();

    /// <summary>Que no llegó ningún aviso. Un cero no lo puede inflar ningún reintento.</summary>
    public void ShouldHaveReceivedNothing() => RequestCount.ShouldBe(0);

    /// <summary>Los cuerpos de los envíos recibidos, en orden.</summary>
    public IReadOnlyList<JsonElement> Sends =>
    [
        .. SendsSinceReset.Select(entry => JsonDocument.Parse(entry.RequestMessage!.Body!).RootElement.Clone()),
    ];

    /// <summary>
    /// Las claves de idempotencia distintas recibidas, en orden de aparición.
    ///
    /// <para>
    /// <b>Es esto y no el conteo de peticiones lo que cuenta "cuántos avisos se mandaron".</b> La
    /// tubería de resiliencia del chasis reintenta, así que un aviso puede llegar dos veces por el
    /// cable cuando la máquina va cargada, y contar peticiones mediría al chasis en vez de a este
    /// servicio. La clave es justamente lo que vuelve inofensivo ese reintento: notification-service
    /// reconoce el duplicado y no manda dos correos.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> SendKeys =>
    [
        .. Sends
            .Select(send => send.GetProperty("idempotencyKey").GetString()!)
            .Distinct(),
    ];

    /// <summary>
    /// El único aviso recibido, por clave de idempotencia. Falla si hubo cero o más de uno; los
    /// reintentos del mismo aviso son uno solo.
    /// </summary>
    public JsonElement SingleSend
    {
        get
        {
            SendKeys.Count.ShouldBe(1, "se esperaba un solo aviso; las claves recibidas fueron: "
                + string.Join(", ", SendKeys));

            return Sends[0];
        }
    }

    /// <summary>
    /// Cuándo llegó cada POST que el servidor ha registrado, sin filtrar por el corte. Solo lo mira
    /// el test del propio stub, para esperar a que aterrice una respuesta tardía; un test de
    /// cotizaciones que lo mirara volvería a contar avisos que no son suyos.
    /// </summary>
    internal IReadOnlyList<DateTime> AllSendArrivals =>
    [
        .. LoggedSends.Select(entry => entry.RequestMessage!.DateTime),
    ];

    /// <summary>
    /// Olvida las peticiones registradas sin tocar lo que se sirve, y mueve el corte: lo que entró
    /// antes de esta línea no es de este test aunque el vecino lo conteste dentro de él.
    /// </summary>
    public void ResetRequestLog()
    {
        _server.ResetLogEntries();
        _countingFrom = DateTime.UtcNow;
    }

    /// <summary>
    /// Vuelve a empezar: se van las reglas y el registro, y el corte se mueve con ellos. Es la
    /// única puerta por la que se reinicia el servidor, para que ningún <c>Serve*</c> nuevo olvide
    /// mover el corte y herede los envíos del test anterior.
    /// </summary>
    private void ResetServer()
    {
        _server.Reset();
        _countingFrom = DateTime.UtcNow;
    }

    /// <summary>El lote se acepta: un 202 con su id.</summary>
    public void ServeAccepted()
    {
        ResetServer();

        _server
            .Given(Request.Create().WithPath(NotificationsPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { batchId = Guid.NewGuid().ToString(), results = Array.Empty<object>() }));
    }

    /// <summary>
    /// El vecino deja de contestar: 500 a todo. No se apaga el servidor para conservar el puerto,
    /// que ya está en la configuración del host y no se puede cambiar en caliente.
    /// </summary>
    public void GoDown()
    {
        ResetServer();

        _server
            .Given(Request.Create().WithPath("/*").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(500));
    }

    /// <summary>
    /// El vecino rechaza la petición entera con un 400. Antes del 202 no existe historia, así que
    /// events-service no tiene puntero que registrar.
    /// </summary>
    public void ServeBadRequest(string code)
    {
        ResetServer();

        _server
            .Given(Request.Create().WithPath(NotificationsPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBodyAsJson(new { code }));
    }

    /// <summary>
    /// El vecino rompe la conexión sin contestar nada: la avería llega como excepción de
    /// transporte y no como un código de estado.
    /// <para>
    /// Es el tercer tipo de fallo que la especificación exige, y es distinto de los otros dos: un
    /// 500 y un 400 son respuestas, y esto no lo es —no hay nada que leer, y la excepción sale del
    /// <c>HttpClient</c> en vez de de <c>EnsureSuccessStatusCode</c>—.
    /// </para>
    /// </summary>
    public void GoFaulty()
    {
        ResetServer();

        _server
            .Given(Request.Create().WithPath("/*").UsingAnyMethod())
            .RespondWith(Response.Create().WithFault(FaultType.EMPTY_RESPONSE));
    }

    /// <summary>
    /// El vecino acepta la conexión y no responde a tiempo. El retraso por omisión supera el tiempo
    /// de espera por intento del chasis (10 s), que es lo que distingue "tardó" de "falló".
    /// <para>
    /// El retraso se puede acortar, y solo lo hace el test del propio stub: esperar quince segundos
    /// para ver llegar una respuesta tardía no prueba nada que no pruebe esperar uno.
    /// </para>
    /// </summary>
    public void GoSlow(TimeSpan? delay = null)
    {
        ResetServer();

        _server
            .Given(Request.Create().WithPath("/*").UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithDelay(delay ?? PastTheChassisAttemptTimeout));
    }

    public void Dispose()
    {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }
}
