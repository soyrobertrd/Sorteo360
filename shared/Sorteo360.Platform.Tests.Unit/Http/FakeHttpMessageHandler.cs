using System.Net;

namespace Sorteo360.Platform.Tests.Unit.Http;

/// <summary>
/// Handler de pruebas que responde con lo que se le programa y guarda cada petición
/// recibida, para poder afirmar sobre la cabecera Authorization, el cuerpo enviado y
/// el número de llamadas sin levantar un servidor.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<int, HttpResponseMessage> _respond;
    private readonly TimeSpan _responseDelay;
    private readonly List<RecordedRequest> _requests = [];
    private readonly Lock _sync = new();

    private FakeHttpMessageHandler(
        Func<int, HttpResponseMessage> respond,
        TimeSpan? responseDelay)
    {
        _respond = respond;
        _responseDelay = responseDelay ?? TimeSpan.Zero;
    }

    /// <summary>Peticiones recibidas, en orden.</summary>
    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_sync)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Número de peticiones recibidas.</summary>
    public int CallCount => Requests.Count;

    /// <summary>
    /// Responde siempre lo mismo. El retardo opcional simula un destino lento, para las
    /// pruebas de tiempo de espera.
    /// </summary>
    public static FakeHttpMessageHandler Always(
        Func<HttpResponseMessage> respond,
        TimeSpan? responseDelay = null) =>
        new(_ => respond(), responseDelay);

    /// <summary>
    /// Responde en función del número de llamada (base 0), para programar secuencias
    /// como "la primera devuelve 401 y la siguiente 200". El retardo opcional sirve para
    /// las pruebas de concurrencia, donde varios llamantes deben coincidir dentro de la
    /// misma respuesta.
    /// </summary>
    public static FakeHttpMessageHandler Sequence(
        Func<int, HttpResponseMessage> respond,
        TimeSpan? responseDelay = null) =>
        new(respond, responseDelay);

    /// <summary>Respuesta JSON con el estado indicado.</summary>
    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        int callIndex;
        lock (_sync)
        {
            callIndex = _requests.Count;
            _requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body));
        }

        if (_responseDelay > TimeSpan.Zero)
        {
            await Task.Delay(_responseDelay, cancellationToken);
        }

        return _respond(callIndex);
    }

    /// <summary>Copia inmutable de una petición recibida.</summary>
    internal sealed record RecordedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? Body);
}
