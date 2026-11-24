using System.Text.Json;
using WireMock;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;
using WireMock.Util;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// Base de los tres vecinos simulados (CRM, eventos y notificaciones), sustituidos por
/// servidores reales que hablan HTTP.
/// <para>
/// Se sustituye el servicio y no el cliente tipado a propósito: lo que hay que ejercitar es
/// la cadena entera —el cliente, la tubería de resiliencia del chasis, la caché y la
/// degradación—, y nada de eso corre si se reemplaza la interfaz por un doble. Es la misma
/// razón que ya explica <c>CrmDirectoryStub</c> en events-service.
/// </para>
/// <para>
/// <b>Lo que reporta es lo del test que corre, y nada más.</b> WireMock apunta una petición en
/// su registro cuando termina de contestarla, no cuando la recibe, así que la que un vecino
/// lento contesta tarde aterriza cuando el test que la mandó ya acabó y el siguiente ya limpió
/// el registro, y pasa por suya. Fue una intermitencia real en events-service (PGTI-32) y acá
/// el andamio es el mismo, así que lo que se mira no es "lo que hay en el registro" sino "lo
/// que llegó después del último corte": la hora de llegada sí es del momento en que la
/// petición entró, y ninguna de este test puede haber entrado antes de que este test empezara.
/// </para>
/// <para>
/// Eso último se apoya en que la llamada al vecino se espera dentro de la petición que la
/// provoca, como pasa hoy: una que saliera por su cuenta —una cola, un proceso de fondo— podría
/// entrar <b>después</b> del corte del test siguiente, y entonces el corte no alcanzaría. El día
/// que eso se construya, esto hay que volver a pensarlo.
/// </para>
/// </summary>
public abstract class NeighbourStub : IDisposable
{
    /// <summary>
    /// Fallos consecutivos que bastan para abrir el cortacircuitos del chasis: diez
    /// intentos con la mitad fallando dentro de la ventana de muestreo. Cada llamada del
    /// cliente son hasta tres intentos (el original y dos reintentos), así que cuatro
    /// llamadas contra un vecino caído alcanzan.
    /// </summary>
    public const int CallsToOpenCircuit = 4;

    /// <summary>Retraso que el chasis ya no espera: su tiempo por intento es de 10 s.</summary>
    private static readonly TimeSpan PastTheChassisAttemptTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// El corte: desde cuándo cuenta lo que llegue. Lo mueve cada limpieza del registro, y con
    /// él deja fuera lo que entró antes aunque se apunte después.
    /// </summary>
    private DateTime _countingFrom = DateTime.UtcNow;

    protected NeighbourStub() => Server = WireMockServer.Start();

    /// <summary>El servidor que sirve las respuestas; las clases derivadas lo programan.</summary>
    protected WireMockServer Server { get; }

    /// <summary>URL base del vecino simulado, tal como la recibe <c>Services__{Nombre}__BaseUrl</c>.</summary>
    public string Url => Server.Url!;

    /// <summary>Cuántas peticiones recibió desde el último <c>ResetRequestLog</c>.</summary>
    public int RequestCount => RequestsSinceReset.Count();

    /// <summary>
    /// Cuándo llegó la primera petición de este test cuya ruta termina en
    /// <paramref name="pathSuffix"/>, o <c>null</c> si no llegó ninguna.
    /// <para>
    /// Es lo único con lo que se puede probar un orden que <b>cruza dos vecinos</b>: cada uno
    /// lleva su propio registro, así que un índice dentro de uno no dice nada del otro y solo la
    /// hora de llegada los pone en la misma línea. Dentro de un mismo vecino basta el orden del
    /// registro, que es lo que ya hace <c>CrmStub.CallsSinceReset</c>.
    /// </para>
    /// </summary>
    /// <param name="pathSuffix">Final de la ruta, tal como la recibe el vecino.</param>
    public DateTime? FirstArrivalEndingIn(string pathSuffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathSuffix);

        return RequestsSinceReset
            .Select(entry => entry.RequestMessage!)
            .Where(request => request.Path.EndsWith(pathSuffix, StringComparison.Ordinal))
            .Select(request => (DateTime?)request.DateTime)
            .OrderBy(arrival => arrival)
            .FirstOrDefault();
    }

    /// <summary>
    /// Cuándo llegó cada petición que el servidor ha registrado, sin filtrar por el corte. Solo
    /// lo mira el test del propio andamio, para esperar a que aterrice una respuesta tardía; un
    /// test de campañas que lo mirara volvería a contar peticiones que no son suyas.
    /// </summary>
    internal IReadOnlyList<DateTime> AllRequestArrivals =>
    [
        .. Server.LogEntries.Select(entry => entry.RequestMessage!.DateTime),
    ];

    /// <summary>
    /// Las peticiones que llegaron después del último corte, que son las de este test. Lo que
    /// un vecino lento contesta tarde conserva su hora de llegada, así que no cuela.
    /// </summary>
    protected IEnumerable<ILogEntry> RequestsSinceReset =>
        Server.LogEntries.Where(entry => entry.RequestMessage!.DateTime >= _countingFrom);

    /// <summary>
    /// Olvida las peticiones registradas sin tocar lo que se sirve, y mueve el corte: lo que
    /// entró antes de esta línea no es de este test aunque el vecino lo conteste dentro de él.
    /// </summary>
    public void ResetRequestLog()
    {
        Server.ResetLogEntries();
        _countingFrom = DateTime.UtcNow;
    }

    /// <summary>Vuelve a lo que sirve por omisión, contador de peticiones incluido.</summary>
    public void ServeDefaults()
    {
        ResetServer();
        ConfigureDefaults();
    }

    /// <summary>
    /// El vecino deja de contestar: 503 a todo. No se apaga el servidor para conservar el
    /// puerto, que ya está en la configuración del host y no se puede cambiar en caliente.
    /// </summary>
    public void GoDown()
    {
        ResetServer();
        Server
            .Given(Request.Create().WithPath("/*").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(503));
    }

    /// <summary>
    /// El vecino acepta la conexión y no responde a tiempo. El retraso por omisión supera el
    /// tiempo de espera por intento del chasis (10 s), que es lo que distingue "tardó" de
    /// "falló".
    /// <para>
    /// El retraso se puede acortar, y solo lo hace el test del propio andamio: esperar quince
    /// segundos para ver llegar una respuesta tardía no prueba nada que no pruebe esperar uno.
    /// </para>
    /// </summary>
    public void GoSlow(TimeSpan? delay = null)
    {
        ResetServer();
        Server
            .Given(Request.Create().WithPath("/*").UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithDelay(delay ?? PastTheChassisAttemptTimeout));
    }

    /// <summary>Lo que sirve el vecino cuando todo va bien. Cada uno declara lo suyo.</summary>
    protected abstract void ConfigureDefaults();

    /// <summary>
    /// Vuelve a empezar: se van las reglas y el registro, y el corte se mueve con ellos. Es la
    /// única puerta por la que se reinicia el servidor, para que ningún <c>Serve*</c> nuevo
    /// olvide mover el corte y herede las peticiones del test anterior.
    /// </summary>
    /// <summary>
    /// Una respuesta JSON armada a mano, que es lo que hace falta desde un callback. Los objetos
    /// anónimos ya se escriben en camelCase, así que el serializador no tiene que renombrar nada.
    /// </summary>
    protected static ResponseMessage Json(int statusCode, object body) => new()
    {
        StatusCode = statusCode,
        Headers = new Dictionary<string, WireMockList<string>>
        {
            ["Content-Type"] = new("application/json"),
        },
        BodyData = new BodyData
        {
            DetectedBodyType = BodyType.String,
            BodyAsString = JsonSerializer.Serialize(body),
        },
    };

    protected void ResetServer()
    {
        Server.Reset();
        _countingFrom = DateTime.UtcNow;
    }

    public void Dispose()
    {
        Server.Dispose();
        GC.SuppressFinalize(this);
    }
}
