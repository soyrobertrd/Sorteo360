using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// El realm visto desde events-service, sustituido por un servidor real que habla HTTP.
/// <para>
/// Se sustituye el realm y no <c>IKeycloakAdminClient</c> por la misma razón que el CRM y
/// notification-service: lo que hay que ejercitar es la cadena entera —el recorrido del grafo, las
/// dos cachés y la degradación—, y nada de eso corre si se reemplaza la interfaz por un doble.
/// </para>
/// <para>
/// <b>Reproduce las dos rarezas que hacen difícil este grafo</b>, porque sin ellas el stub sería
/// más amable que Keycloak y los tests pasarían contra un código que en producción no avisa a
/// nadie: pedir los que tienen <c>quotes:approve</c> devuelve la lista vacía —solo contesta
/// asignaciones directas—, y el <c>?q=</c> del endpoint de miembros se ignora en silencio, así que
/// devuelve todos sin filtrar.
/// </para>
/// </summary>
public sealed class KeycloakRealmStub : IDisposable
{
    private const string Realm = "sorteo360";
    private const string ApproveRole = "quotes:approve";

    /// <summary>
    /// El compuesto que contiene el rol buscado. Vive solo acá y nunca en el código de
    /// producción: ese es justamente el punto del recorrido del grafo.
    /// </summary>
    private const string DirectorRole = "opt_director";

    private readonly WireMockServer _server = WireMockServer.Start();

    public KeycloakRealmStub() => ServeDefaults();

    /// <summary>URL base, tal como la recibe <c>Services__Keycloak__BaseUrl</c>.</summary>
    public string Url => _server.Url!;

    /// <summary>
    /// Un realm sano: una persona de prueba del directorio de operadores.
    /// </summary>
    public void ServeDefaults() => Serve(
        new StubRealmUser("u-ana", "ana", "ana@sorteo360.local", "Ana", "Pérez", CrmDirectoryStub.OfficeCode));

    /// <summary>
    /// Publica este realm. Cada persona tiene el rol por el compuesto; el <c>opt_code</c> decide a
    /// qué oficina pertenece, y la intersección la hace events-service.
    /// </summary>
    public void Serve(params StubRealmUser[] users)
    {
        _server.Reset();

        // 1. El listado de roles, con la bandera de compuesto. De acá sale a quién pedirle hijos.
        _server
            .Given(Request.Create().WithPath($"/admin/realms/{Realm}/roles").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new object[]
                {
                    new { name = ApproveRole, composite = false },
                    new { name = DirectorRole, composite = true },
                    new { name = "opt_coordinator", composite = true },
                }));

        // 2. Los hijos de cada compuesto. opt_director contiene el rol que se busca; el otro no,
        //    para que el recorrido tenga algo que descartar.
        _server
            .Given(Request.Create().WithPath($"/admin/realms/{Realm}/roles/{DirectorRole}/composites/realm").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new object[] { new { name = ApproveRole, composite = false } }));

        _server
            .Given(Request.Create().WithPath($"/admin/realms/{Realm}/roles/opt_coordinator/composites/realm").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new object[] { new { name = "quotes:write", composite = false } }));

        // 3. Quién tiene el rol buscado directamente: nadie. Es la respuesta real de Keycloak y la
        //    razón por la que hay que invertir el grafo en vez de preguntar de frente.
        _server
            .Given(Request.Create().WithPath($"/admin/realms/{Realm}/roles/{ApproveRole}/users").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(Array.Empty<object>()));

        // 4. Quién tiene el compuesto. El ?q= de esta ruta Keycloak lo ignora en silencio, así que
        //    acá se devuelve todo el mundo sin filtrar: si la intersección no ocurriera del lado de
        //    events-service, este test mandaría correo a las dieciocho oficinas.
        _server
            .Given(Request.Create().WithPath($"/admin/realms/{Realm}/roles/{DirectorRole}/users").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(users.Select(ToUser).ToArray()));

        // 5. La búsqueda por atributo, que sí filtra. Es el otro lado de la intersección.
        foreach (var optCode in users.Select(u => u.OptCode).Where(c => c is not null).Distinct())
        {
            _server
                .Given(Request.Create()
                    .WithPath($"/admin/realms/{Realm}/users")
                    .WithParam("q", $"opt_code:{optCode}")
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBodyAsJson(users.Where(u => u.OptCode == optCode).Select(ToUser).ToArray()));
        }

        // Cualquier otro opt_code no tiene a nadie: es el caso Chile, que se siembra sin dirección.
        _server
            .Given(Request.Create().WithPath($"/admin/realms/{Realm}/users").UsingGet())
            .AtPriority(10)
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(Array.Empty<object>()));

        // 6. La lectura por id, que es por donde se resuelve quien sometió un intento. Es otra ruta
        //    y no el mismo listado con un filtro: acá no hay nada que deducir, el id ya se tiene.
        foreach (var user in users)
        {
            _server
                .Given(Request.Create().WithPath($"/admin/realms/{Realm}/users/{user.Id}").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBodyAsJson(ToUser(user)));
        }

        // Un id que el realm no conoce contesta 404, que es lo que distingue "esta cuenta ya no
        // está" de "el realm no contesta". Sin esta regla el stub daría 404 igual, pero por no
        // tener nada configurado, y entonces el test no estaría afirmando nada.
        _server
            .Given(Request.Create().WithPath($"/admin/realms/{Realm}/users/*").UsingGet())
            .AtPriority(10)
            .RespondWith(Response.Create().WithStatusCode(404));
    }

    /// <summary>
    /// La lectura de esa persona por id contesta <c>500</c>: el realm está, pero no puede decir quién
    /// es. Se suma a lo publicado y le gana a la regla del 404.
    /// </summary>
    public void FailUser(string userId) =>
        _server
            .Given(Request.Create().WithPath($"/admin/realms/{Realm}/users/{userId}").UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create().WithStatusCode(500));

    /// <summary>El realm deja de contestar: 503 a todo, que con la caché fría deja la lista vacía.</summary>
    public void GoDown()
    {
        _server.Reset();

        _server
            .Given(Request.Create().WithPath("/*").UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(503));
    }

    private static object ToUser(StubRealmUser user) => new
    {
        id = user.Id,
        username = user.Username,
        email = user.Email,
        firstName = user.FirstName,
        lastName = user.LastName,
        enabled = user.Enabled,
        // El opt_code no viaja de vuelta, igual que en el realm real: es un atributo no gestionado
        // y la política por defecto lo filtra de toda respuesta. Sirve para filtrar y es invisible
        // al leerlo, y todo el enfoque sobrevive porque nunca se lee de vuelta.
    };

    public void Dispose()
    {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Una persona del realm simulado.</summary>
public sealed record StubRealmUser(
    string Id,
    string Username,
    string? Email,
    string FirstName,
    string LastName,
    string? OptCode,
    bool Enabled = true);
