using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// notification-service visto desde convocatorias-service: el 202 por lote con un resultado
/// por destinatario.
/// <para>
/// Los tests van contra este servidor por el transporte <c>http</c> y no contra el gateway
/// de stub, por la misma razón que el resto: hay que ejercitar la cadena entera. El gateway
/// de stub es un componente de producción y lleva sus propios tests, aparte de estos.
/// </para>
/// </summary>
public sealed class NotificationStub : NeighbourStub
{
    private const string NotificationsPath = "/v1/notifications";

    public const string AcceptedStatus = "accepted";
    public const string RejectedStatus = "rejected";
    public const string InvalidAddressReason = "RECIPIENT_ADDRESS_INVALID";

    /// <summary>Direcciones que el proveedor rechaza; las demás se aceptan.</summary>
    private readonly HashSet<string> _rejectedRecipients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Lo que el historial contesta por id de mensaje: la entrega y la reacción.</summary>
    private readonly Dictionary<Guid, (string DeliveryStatus, string RecipientAction)> _delivery = [];

    /// <summary>El último id de mensaje que el 202 devolvió por cada dirección aceptada.</summary>
    public Dictionary<string, Guid> IssuedIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// El historial dirá esto de ese mensaje en <c>GET /v1/notifications?id=</c> (PGTI-177). Un id
    /// que no se declaró no aparece en la respuesta, como un envío ajeno al alcance.
    /// </summary>
    public void ServeDelivery(Guid notificationId, string deliveryStatus, string recipientAction = "none") =>
        _delivery[notificationId] = (deliveryStatus, recipientAction);

    public NotificationStub() => ConfigureDefaults();

    /// <summary>
    /// Los cuerpos de los lotes que llegaron en este test, en orden. Es lo que deja afirmar sobre
    /// el JSON del cable sin doblar el cliente: lo que se mira es lo que salió de verdad por el
    /// <c>HttpClient</c> que arma el chasis.
    /// </summary>
    public IReadOnlyList<string> PostedBatches =>
    [
        .. RequestsSinceReset
            .Select(entry => entry.RequestMessage!)
            .Where(request => request.Path.EndsWith(NotificationsPath, StringComparison.Ordinal))
            .OrderBy(request => request.DateTime)
            .Select(request => request.Body ?? string.Empty),
    ];

    /// <summary>
    /// Lo que se le manda es un 503, como si el servicio estuviera caído, pero solo a partir de la
    /// llamada <paramref name="fromCall"/> de este test. Es cómo se prueba que un lote que se cae
    /// deja sus filas <c>pending</c> sin tocar las que ya se entregaron (spec <c>04</c> §2.1).
    /// </summary>
    public void ServeUnavailableFromCall(int fromCall)
    {
        ResetServer();

        var seen = 0;

        Server
            .Given(Request.Create().WithPath(NotificationsPath).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithCallback(request =>
                {
                    var call = Interlocked.Increment(ref seen);

                    if (call >= fromCall)
                        return new WireMock.ResponseMessage { StatusCode = 503 };

                    return new WireMock.ResponseMessage
                    {
                        StatusCode = 202,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            ["Content-Type"] = new("application/json"),
                            ["Location"] = new($"{NotificationsPath}/batches/{Guid.NewGuid()}"),
                        },
                        BodyData = new WireMock.Util.BodyData
                        {
                            DetectedBodyType = WireMock.Types.BodyType.String,
                            BodyAsString = BuildBatchResponse(request.Body),
                        },
                    };
                }));
    }

    /// <inheritdoc />
    protected override void ConfigureDefaults()
    {
        _rejectedRecipients.Clear();
        _delivery.Clear();
        ServeBatchAccepted();
    }

    /// <summary>
    /// El lote se acepta: un 202 con una entrada por destinatario, aceptada salvo las
    /// direcciones marcadas con <see cref="Reject"/>.
    /// </summary>
    public void ServeBatchAccepted()
    {
        ResetServer();

        Server
            .Given(Request.Create().WithPath(NotificationsPath).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithCallback(request => new WireMock.ResponseMessage
                {
                    StatusCode = 202,
                    Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                    {
                        ["Content-Type"] = new("application/json"),
                        ["Location"] = new($"{NotificationsPath}/batches/{Guid.NewGuid()}"),
                    },
                    BodyData = new WireMock.Util.BodyData
                    {
                        DetectedBodyType = WireMock.Types.BodyType.String,
                        BodyAsString = BuildBatchResponse(request.Body),
                    },
                }));

        Server
            .Given(Request.Create().WithPath(NotificationsPath).UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithCallback(request => new WireMock.ResponseMessage
                {
                    StatusCode = 200,
                    Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                    {
                        ["Content-Type"] = new("application/json"),
                    },
                    BodyData = new WireMock.Util.BodyData
                    {
                        DetectedBodyType = WireMock.Types.BodyType.String,
                        BodyAsString = BuildHistoryResponse(request.Query),
                    },
                }));
    }

    /// <summary>
    /// Esta dirección vuelve como <c>rejected</c> dentro de un 202. Es terminal para esa
    /// persona: no es una avería del lote, es que ese destinatario no se puede entregar.
    /// </summary>
    public void Reject(string recipientEmail) => _rejectedRecipients.Add(recipientEmail);

    /// <summary>
    /// La petición entera se rechaza con 400. Antes del 202 no existe historia, así que
    /// convocatorias no debe registrar nada.
    /// </summary>
    public void ServeBadRequest(string code)
    {
        ResetServer();

        Server
            .Given(Request.Create().WithPath(NotificationsPath).UsingPost())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBodyAsJson(new { code }));
    }

    /// <summary>La página del historial con las filas declaradas de los ids que se pidieron.</summary>
    private string BuildHistoryResponse(IDictionary<string, WireMock.Types.WireMockList<string>>? query)
    {
        var items = (query is not null && query.TryGetValue("id", out var ids) ? ids : [])
            .Select(Guid.Parse)
            .Where(_delivery.ContainsKey)
            .Select(id => new { id, deliveryStatus = _delivery[id].DeliveryStatus, recipientAction = _delivery[id].RecipientAction })
            .ToList();

        return JsonSerializer.Serialize(new { total = items.Count, page = 1, pageSize = 100, items });
    }

    /// <summary>Arma el 202 leyendo los destinatarios que trajo la petición.</summary>
    private string BuildBatchResponse(string? requestBody)
    {
        var recipients = ReadRecipients(requestBody);

        var results = recipients.Select(email => _rejectedRecipients.Contains(email)
            ? new { id = (string?)null, recipient = email, status = RejectedStatus, reason = (string?)InvalidAddressReason }
            : new { id = (string?)Issue(email).ToString(), recipient = email, status = AcceptedStatus, reason = (string?)null });

        return JsonSerializer.Serialize(new
        {
            batchId = Guid.NewGuid().ToString(),
            results,
        });
    }

    private Guid Issue(string email)
    {
        var id = Guid.NewGuid();
        lock (IssuedIds)
            IssuedIds[email] = id;
        return id;
    }

    private static List<string> ReadRecipients(string? requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            return [];

        using var document = JsonDocument.Parse(requestBody);
        if (!document.RootElement.TryGetProperty("recipients", out var recipients))
            return [];

        return [.. recipients
            .EnumerateArray()
            .Select(recipient => recipient.TryGetProperty("email", out var email) ? email.GetString() : null)
            .Where(email => email is not null)
            .Select(email => email!)];
    }
}
