using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// El endpoint de token del realm, sustituido por un servidor real que habla HTTP. Es lo que
/// el relevo de tokens llama por detrás, así que lo que se prueba es la cadena entera: el
/// cuerpo que sale hacia Keycloak, el estado que vuelve y lo que el relevo contesta con él.
/// <para>
/// Contesta como Keycloak: <c>200</c> con el JSON del token si el formulario trae
/// <c>client_credentials</c> y las credenciales que se le dieron, y <c>401</c> con
/// <c>invalid_client</c> a cualquier otra cosa.
/// </para>
/// </summary>
public sealed class KeycloakTokenStub : IDisposable
{
    /// <summary>Ruta del endpoint de token dentro del realm.</summary>
    private const string TokenPath = "/realms/pgti360/protocol/openid-connect/token";

    private readonly WireMockServer _server = WireMockServer.Start();

    /// <summary>La URL del realm, tal como la recibe <c>Keycloak__Authority</c>.</summary>
    public string Authority => $"{_server.Url}/realms/pgti360";

    /// <summary>
    /// El cuerpo exacto que devuelve el realm al acertar. Lleva un campo que el relevo no
    /// conoce, para que un test pueda comprobar que se copia sin tocar y no se reescribe.
    /// </summary>
    public static string TokenBody(string accessToken) =>
        $$"""{"access_token":"{{accessToken}}","expires_in":300,"refresh_expires_in":0,"token_type":"Bearer","not-before-policy":0,"scope":"profile email"}""";

    /// <summary>Lo que el realm recibió, en orden de llegada.</summary>
    public IReadOnlyList<ILogEntry> Requests => _server.LogEntries.ToList();

    /// <summary>
    /// Publica un realm sano que conoce a <paramref name="clientId"/> con
    /// <paramref name="clientSecret"/> y le entrega <paramref name="accessToken"/>.
    /// </summary>
    public void Accept(string clientId, string clientSecret, string accessToken)
    {
        _server.Reset();

        _server
            .Given(Request.Create()
                .WithPath(TokenPath)
                .UsingPost()
                .WithHeader("Content-Type", "application/x-www-form-urlencoded*")
                .WithBody(body => IsClientCredentials(body, clientId, clientSecret)))
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(TokenBody(accessToken)));

        _server
            .Given(Request.Create().WithPath(TokenPath).UsingPost())
            .AtPriority(10)
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"error":"invalid_client","error_description":"Invalid client or Invalid client credentials"}"""));
    }

    /// <summary>El realm deja de contestar como debe: 503 a todo.</summary>
    public void GoDown()
    {
        _server.Reset();

        _server
            .Given(Request.Create().WithPath("/*").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(503));
    }

    /// <summary>
    /// El formulario trae exactamente el grant de cliente y esas credenciales. Se compara por
    /// campos y no por la cadena entera, porque el orden de los campos no es contrato de nadie.
    /// </summary>
    private static bool IsClientCredentials(string? body, string clientId, string clientSecret)
    {
        if (body is null)
            return false;

        var fields = ParseForm(body);

        return fields.Count == 3
            && fields.GetValueOrDefault("grant_type") == "client_credentials"
            && fields.GetValueOrDefault("client_id") == clientId
            && fields.GetValueOrDefault("client_secret") == clientSecret;
    }

    /// <summary>Los campos de un cuerpo <c>application/x-www-form-urlencoded</c>.</summary>
    public static Dictionary<string, string> ParseForm(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty,
                StringComparer.Ordinal);

    public void Dispose()
    {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }
}
