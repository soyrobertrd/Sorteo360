using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Infrastructure.Keycloak;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Keycloak;

/// <summary>
/// Lo que se fija acá es la ruta y la paginación, que es donde esta lectura se equivoca en silencio.
/// Los dos endpoints paginados devuelven 100 por defecto y no existe ninguno que diga cuántos hay:
/// pedir sin paginar trae los primeros cien y contesta 200, así que la respuesta equivocada no se
/// distingue de la correcta mirando el código de estado.
/// </summary>
public class KeycloakAdminClientTests
{
    private readonly RecordingHandler _handler = new();
    private readonly QuoteApproverSettings _settings = new() { Realm = "pgti360", PageSize = 2 };

    private KeycloakAdminClient CreateSut() =>
        new(new HttpClient(_handler) { BaseAddress = new Uri("http://keycloak:8080/") },
            Options.Create(_settings));

    #region Paginación

    [Fact]
    public async Task ListRealmRolesAsync_ReturnsEveryPage_WhenTheLastPageComesBackShort()
    {
        _handler.Respond("""[{"name":"a","composite":false},{"name":"b","composite":true}]""");
        _handler.Respond("""[{"name":"c","composite":false}]""");

        var roles = await CreateSut().ListRealmRolesAsync(CancellationToken.None);

        roles.Select(r => r.Name).ShouldBe(["a", "b", "c"]);
        _handler.Requests.Count.ShouldBe(2);
        _handler.Requests[0].ShouldContain("first=0&max=2");
        _handler.Requests[1].ShouldContain("first=2&max=2");
    }

    [Fact]
    public async Task GetRealmRoleMembersAsync_ReturnsEveryPage_WhenTheLastPageComesBackShort()
    {
        _handler.Respond("""[{"id":"u1","enabled":true},{"id":"u2","enabled":true}]""");
        _handler.Respond("[]");

        var users = await CreateSut().GetRealmRoleMembersAsync("opt_director", CancellationToken.None);

        users.Select(u => u.Id).ShouldBe(["u1", "u2"]);
        _handler.Requests.Count.ShouldBe(2);
        _handler.Requests[1].ShouldContain("first=2&max=2");
    }

    [Fact]
    public async Task FindUsersByAttributeAsync_ReturnsEveryPage_WhenTheLastPageComesBackShort()
    {
        _handler.Respond("""[{"id":"u1","enabled":true},{"id":"u2","enabled":true}]""");
        _handler.Respond("""[{"id":"u3","enabled":true}]""");

        var users = await CreateSut().FindUsersByAttributeAsync("opt_code", "miami", CancellationToken.None);

        users.Select(u => u.Id).ShouldBe(["u1", "u2", "u3"]);
        _handler.Requests.Count.ShouldBe(2);
    }

    #endregion

    #region Rutas

    [Fact]
    public async Task GetRealmRoleMembersAsync_ReturnsARequestWithTheColonEscaped_WhenTheRoleNameHasAColon()
    {
        // Sin escapar, los dos puntos de quotes:approve cambian lo que Keycloak entiende por la
        // ruta, y el resultado es un 404 o una lista vacía que parece una respuesta legítima.
        _handler.Respond("[]");

        await CreateSut().GetRealmRoleMembersAsync("quotes:approve", CancellationToken.None);

        _handler.Requests[0].ShouldContain("/admin/realms/pgti360/roles/quotes%3Aapprove/users");
    }

    [Fact]
    public async Task FindUsersByAttributeAsync_ReturnsARequestWithTheAttributeAsAQueryFilter_WhenCalled()
    {
        _handler.Respond("[]");

        await CreateSut().FindUsersByAttributeAsync("opt_code", "miami", CancellationToken.None);

        _handler.Requests[0].ShouldContain("/admin/realms/pgti360/users?q=opt_code%3Amiami");
    }

    [Fact]
    public async Task GetRealmRoleCompositesAsync_ReturnsOneRequestForRealmCompositesOnly_WhenCalled()
    {
        _handler.Respond("[]");

        await CreateSut().GetRealmRoleCompositesAsync("opt_director", CancellationToken.None);

        _handler.Requests[0].ShouldContain("/admin/realms/pgti360/roles/opt_director/composites/realm");
        _handler.Requests.Count.ShouldBe(1);
    }

    #endregion

    #region Fallos

    [Fact]
    public async Task ListRealmRolesAsync_ThrowsHttpRequestException_WhenTheRealmRefuses()
    {
        // Un 403 es lo que contesta el realm mientras la cuenta de servicio no tenga view-realm.
        // Sale como HttpRequestException, que es lo que el chasis garantiza y lo que quien llama
        // sabe degradar.
        _handler.Respond("", HttpStatusCode.Forbidden);

        await Should.ThrowAsync<HttpRequestException>(
            () => CreateSut().ListRealmRolesAsync(CancellationToken.None));
    }

    #endregion

    /// <summary>
    /// Un manejador que anota cada URL pedida y contesta las respuestas encoladas, para poder mirar
    /// la paginación sin levantar nada.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<(string Body, HttpStatusCode Status)> _responses = new();

        public List<string> Requests { get; } = [];

        public void Respond(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            _responses.Enqueue((body, status));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());

            var (body, status) = _responses.Count > 0 ? _responses.Dequeue() : ("[]", HttpStatusCode.OK);

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
