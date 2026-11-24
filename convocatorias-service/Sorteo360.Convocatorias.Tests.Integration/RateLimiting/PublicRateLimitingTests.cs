using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.RateLimiting;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.RateLimiting;

/// <summary>
/// El límite por IP del prefijo público, medido a través de los dos saltos de proxy que hay
/// en producción: Traefik y el nginx del frontend.
/// <para>
/// Las rutas de abajo no existen todavía —el portal llega en su propia especificación— y da
/// igual: el limitador corre sobre la tubería, antes de que se sepa si hay endpoint, así que
/// una inundación se corta aunque apunte a una ruta inventada.
/// </para>
/// </summary>
public sealed class PublicRateLimitingTests : IClassFixture<ConvocatoriasApiFactory>
{
    private const string PublicPath = "/v1/public/events";
    private const string InternalPath = "/v1/convocatorias/campaigns";

    /// <summary>El permitido por ventana. Una más es la que tiene que rebotar.</summary>
    private const int PerIpPermitLimit = PublicRateLimiting.PerIpPermitLimit;

    /// <summary>La cadena que arma nginx: primero el cliente, después el proxy anterior.</summary>
    private const string ProxyHop = "172.20.0.5";

    private readonly ConvocatoriasApiFactory _factory;

    public PublicRateLimitingTests(ConvocatoriasApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PublicPrefix_Returns429WithItsCode_WhenOneClientIpFloods()
    {
        var client = _factory.CreateClient();
        const string clientIp = "203.0.113.10";

        for (var i = 0; i < PerIpPermitLimit; i++)
        {
            var allowed = await client.SendAsync(Probe(PublicPath, clientIp));
            allowed.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }

        var rejected = await client.SendAsync(Probe(PublicPath, clientIp));

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();

        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe(PublicRateLimiting.RateLimitedCode);
    }

    [Fact]
    public async Task PublicPrefix_Returns429OnlyToTheFloodingIp_WhenBehindTheSameProxy()
    {
        var client = _factory.CreateClient();
        const string floodingIp = "203.0.113.20";
        const string innocentIp = "203.0.113.21";

        for (var i = 0; i <= PerIpPermitLimit; i++)
        {
            await client.SendAsync(Probe(PublicPath, floodingIp));
        }

        // Misma conexión, mismo proxy: lo único distinto es quién llama de verdad.
        var flooded = await client.SendAsync(Probe(PublicPath, floodingIp));
        var innocent = await client.SendAsync(Probe(PublicPath, innocentIp));

        flooded.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        innocent.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task InternalPrefix_ReturnsNo429_WhenOneClientIpFloods()
    {
        var client = _factory.CreateClient();
        const string clientIp = "203.0.113.30";

        for (var i = 0; i <= PerIpPermitLimit; i++)
        {
            var response = await client.SendAsync(Probe(InternalPath, clientIp));
            response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }
    }

    /// <summary>
    /// El eje por correo reparte por la dirección del cuerpo y no por la IP (spec <c>06</c> §3).
    /// <para>
    /// <b>Es la diferencia entre un límite que sirve y uno que estorba.</b> Lo que este eje
    /// atrapa es usar el portal para bombardear a un tercero reenviando su dirección, así que su
    /// tope es estrecho; si repartiera por IP, una oficina entera inscribiéndose desde una sola
    /// salida a internet rebotaría a la sexta persona.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Register_Returns429OnlyToTheBombardedAddress_WhenClientsShareOneIp()
    {
        var client = _factory.CreateClient();
        const string sharedIp = "203.0.113.40";
        const string bombarded = "victima@acme.com";

        // Bastante más que el tope por correo, y bastante menos que el global por IP.
        for (var i = 0; i < 10; i++)
        {
            await client.SendAsync(Submit(bombarded, sharedIp));
        }

        var bombing = await client.SendAsync(Submit(bombarded, sharedIp));
        var colleague = await client.SendAsync(Submit("otra@acme.com", sharedIp));

        bombing.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        colleague.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Y lo hace sobre la dirección normalizada: con una mayúscula de diferencia el eje se
    /// esquivaría cambiando cómo se escribe la misma dirección.
    /// </summary>
    [Fact]
    public async Task Register_Returns429_WhenTheSameAddressIsWrittenDifferently()
    {
        var client = _factory.CreateClient();
        const string sharedIp = "203.0.113.41";

        for (var i = 0; i < 10; i++)
        {
            await client.SendAsync(Submit("objetivo@acme.com", sharedIp));
        }

        var shouted = await client.SendAsync(Submit("  OBJETIVO@ACME.COM ", sharedIp));

        shouted.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// <b>Y no se esquiva escribiendo <c>Email</c> con mayúscula.</b> El binder de Minimal APIs
    /// no distingue la caja del nombre del campo, así que un cuerpo así se inscribe igual; si
    /// esta lectura sí la distinguiera, quedaría sin clave y repartiendo por IP, que es
    /// exactamente el eje que se quería esquivar.
    /// </summary>
    [Fact]
    public async Task Register_Returns429_WhenTheEmailPropertyNameChangesCase()
    {
        var client = _factory.CreateClient();
        const string sharedIp = "203.0.113.42";

        for (var i = 0; i < 10; i++)
        {
            await client.SendAsync(Submit("mismo@acme.com", sharedIp, propertyName: "email"));
        }

        var shouted = await client.SendAsync(Submit("mismo@acme.com", sharedIp, propertyName: "Email"));

        shouted.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Regresión de la auditoría run-1 (confirmado 7): con <c>email</c> dos veces, el limitador
    /// leía la primera y el binder inscribía la última, así que el eje por correo se esquivaba
    /// variando la primera. Ahora el cuerpo se rechaza antes de inscribir a nadie.
    /// </summary>
    [Theory]
    [InlineData("email", "email")]
    [InlineData("email", "Email")]
    public async Task Register_Returns400_WhenTheBodyRepeatsTheEmailKey(string first, string second)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/public/registrations")
        {
            Content = new StringContent(
                $$"""{"eventId":"{{Guid.NewGuid()}}","firstName":"Ana","lastName":"Reyes","{{first}}":"senuelo-{{Guid.NewGuid():N}}@acme.com","{{second}}":"victima@acme.com"}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("X-Forwarded-For", $"203.0.113.46, {ProxyHop}");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// El eje por token, sobre <c>resolve</c>: la enumeración de tokens que rota de IP y que el
    /// eje global no ve. Reparte por el token y no por quien llama.
    /// </summary>
    [Fact]
    public async Task Resolve_Returns429OnlyToTheFloodedToken_WhenClientsShareOneIp()
    {
        var client = _factory.CreateClient();
        const string sharedIp = "203.0.113.43";

        for (var i = 0; i < 15; i++)
        {
            await client.SendAsync(Resolve("un-token-cualquiera", sharedIp));
        }

        var enumerating = await client.SendAsync(Resolve("un-token-cualquiera", sharedIp));
        var somebodyElse = await client.SendAsync(Resolve("el-token-de-otra-persona", sharedIp));

        enumerating.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        somebodyElse.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// El eje por token también cubre la baja: reparte por el token, no por la IP, así que la
    /// enumeración que rota de IP se corta y quien tiene el suyo no rebota por la de al lado.
    /// </summary>
    [Fact]
    public async Task Unsubscribe_IsRejected_WhenOneTokenFloodsFromRotatingIps()
    {
        var client = _factory.CreateClient();
        const string sharedIp = "203.0.113.44";

        for (var i = 0; i < 15; i++)
        {
            await client.SendAsync(Unsubscribe("un-token-de-baja-cualquiera", sharedIp));
        }

        var enumerating = await client.SendAsync(Unsubscribe("un-token-de-baja-cualquiera", sharedIp));
        var somebodyElse = await client.SendAsync(Unsubscribe("el-token-de-baja-de-otra", sharedIp));

        enumerating.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        somebodyElse.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// El eje por correo <b>no</b> cubre la baja (spec <c>06</c> §3): a diferencia de la
    /// inscripción, no manda ningún correo, así que no hay con qué bombardear a un tercero y su
    /// tope estrecho estorbaría. Ocho bajas desde una misma IP, cada una con su token, no rebotan:
    /// si el eje por correo estuviera enganchado, al no haber dirección en el cuerpo repartiría
    /// por IP con tope cinco, y la sexta caería.
    /// </summary>
    [Fact]
    public async Task Unsubscribe_IsNotRejected_WhenManyClicksShareOneIpUnderDistinctTokens()
    {
        var client = _factory.CreateClient();
        const string sharedIp = "203.0.113.45";

        for (var i = 0; i < 8; i++)
        {
            var response = await client.SendAsync(Unsubscribe($"token-de-baja-numero-{i}", sharedIp));
            response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }
    }

    /// <summary>Una baja, para los ejes de la ruta de baja.</summary>
    private static HttpRequestMessage Unsubscribe(string token, string clientIp) =>
        Posted(
            "/v1/public/unsubscribe",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["token"] = token },
            clientIp);

    /// <summary>
    /// Un envío del formulario. Lo que se afirma es el <c>429</c>, así que da igual que detrás
    /// no haya evento: el limitador corre antes de que se sepa qué contesta el endpoint.
    /// </summary>
    private static HttpRequestMessage Submit(string email, string clientIp, string propertyName = "email")
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["eventId"] = Guid.NewGuid(),
            ["firstName"] = "Ana",
            ["lastName"] = "Reyes",
            [propertyName] = email,
        };

        return Posted("/v1/public/registrations", body, clientIp);
    }

    /// <summary>Una resolución de enlace, para el eje por token.</summary>
    private static HttpRequestMessage Resolve(string token, string clientIp) =>
        Posted(
            "/v1/public/registrations/resolve",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["token"] = token },
            clientIp);

    private static HttpRequestMessage Posted(string path, object body, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };

        request.Headers.Add("X-Forwarded-For", $"{clientIp}, {ProxyHop}");
        return request;
    }

    private static HttpRequestMessage Probe(string path, string clientIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-For", $"{clientIp}, {ProxyHop}");
        return request;
    }
}
