using System.Net;
using System.Text.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Platform.Auth;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.OpenApi;

/// <summary>
/// Contrato del documento OpenAPI que sirve de fuente de verdad de la API (/scalar lo
/// renderiza). Verifica presencia y estructura, nunca el texto de la prosa: reescribir
/// una descripción no debe romper un test, pero publicar un endpoint sin documentar sí.
/// </summary>
public sealed class OpenApiDocumentTests : IClassFixture<EventsApiFactory>
{
    private static readonly string[] HttpMethods =
        ["get", "post", "put", "patch", "delete"];

    private static readonly string[] ExpectedTags =
        ["Events", "Cities", "Files"];

    private readonly HttpClient _client;

    public OpenApiDocumentTests(EventsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Documento

    [Fact]
    public async Task Document_IsServed_WhenTheCallerIsAnonymous()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Document_HasADescription_WhenTheDocumentIsServed()
    {
        var document = await GetDocumentAsync();

        var description = document.RootElement
            .GetProperty("info")
            .GetProperty("description")
            .GetString();

        description.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EveryTag_HasADescription_WhenTheDocumentIsServed()
    {
        var document = await GetDocumentAsync();

        var described = document.RootElement
            .GetProperty("tags")
            .EnumerateArray()
            .Where(t => t.TryGetProperty("description", out var d) && !string.IsNullOrWhiteSpace(d.GetString()))
            .Select(t => t.GetProperty("name").GetString())
            .ToList();

        foreach (var tag in ExpectedTags)
            described.ShouldContain(tag);
    }

    /// <summary>
    /// Sin el esquema declarado, Scalar no muestra el panel de autenticación y toda
    /// prueba desde la UI responde 401 aunque el endpoint funcione.
    /// </summary>
    [Fact]
    public async Task Document_DeclaresTheBearerSecurityScheme_WhenTheDocumentIsServed()
    {
        var document = await GetDocumentAsync();

        var scheme = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty(KeycloakOpenApiSecurity.SchemeName);

        scheme.GetProperty("type").GetString().ShouldBe("http");
        scheme.GetProperty("scheme").GetString().ShouldBe("bearer");
        scheme.GetProperty("bearerFormat").GetString().ShouldBe("JWT");
    }

    /// <summary>
    /// El requisito es global porque la fallback policy exige usuario autenticado en todo
    /// endpoint sin AllowAnonymous, y los anónimos quedan fuera del documento.
    /// </summary>
    [Fact]
    public async Task Document_RequiresTheBearerSchemeGlobally_WhenTheDocumentIsServed()
    {
        var document = await GetDocumentAsync();

        var required = document.RootElement
            .GetProperty("security")
            .EnumerateArray()
            .SelectMany(requirement => requirement.EnumerateObject())
            .Select(entry => entry.Name)
            .ToList();

        required.ShouldContain(KeycloakOpenApiSecurity.SchemeName);
    }

    #endregion

    #region Operaciones

    [Fact]
    public async Task EveryOperation_HasASummary_WhenTheDocumentIsServed()
    {
        var document = await GetDocumentAsync();

        var undocumented = Operations(document)
            .Where(o => !o.Element.TryGetProperty("summary", out var s) || string.IsNullOrWhiteSpace(s.GetString()))
            .Select(o => o.Name)
            .ToList();

        undocumented.ShouldBeEmpty(
            $"Estas operaciones no tienen summary: {string.Join(", ", undocumented)}");
    }

    [Fact]
    public async Task EveryOperation_DeclaresASuccessResponse_WhenTheDocumentIsServed()
    {
        var document = await GetDocumentAsync();

        var missing = Operations(document)
            .Where(o => !o.Element.GetProperty("responses")
                .EnumerateObject()
                .Any(r => r.Name.StartsWith('2')))
            .Select(o => o.Name)
            .ToList();

        missing.ShouldBeEmpty(
            $"Estas operaciones no declaran respuesta de éxito: {string.Join(", ", missing)}");
    }

    [Fact]
    public async Task EveryOperation_DeclaresUnauthorized_WhenTheDocumentIsServed()
    {
        var document = await GetDocumentAsync();

        var missing = Operations(document)
            .Where(o => !o.Element.GetProperty("responses")
                .EnumerateObject()
                .Any(r => r.Name == "401"))
            .Select(o => o.Name)
            .ToList();

        missing.ShouldBeEmpty(
            $"Estas operaciones no declaran 401: {string.Join(", ", missing)}");
    }

    [Fact]
    public async Task EveryOperation_HasAnAccessLineInItsDescription_WhenTheDocumentIsServed()
    {
        var document = await GetDocumentAsync();

        var missing = Operations(document)
            .Where(o => !HasAccessLine(o.Element))
            .Select(o => o.Name)
            .ToList();

        missing.ShouldBeEmpty(
            $"El transformer de rol no anexó la línea de acceso a: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Una operación lleva línea de acceso si su descripción nombra un rol exigido,
    /// declara que basta con estar autenticado o que la ruta es anónima. Comprobar solo que la descripción no
    /// esté vacía dejaría pasar un transformer desconectado en los endpoints que ya
    /// traen prosa propia.
    /// </summary>
    private static bool HasAccessLine(JsonElement operation)
    {
        if (!operation.TryGetProperty("description", out var element) || element.GetString() is not { } description)
            return false;

        return description.Contains("Rol requerido:", StringComparison.Ordinal)
            || description.Contains("requiere usuario autenticado", StringComparison.Ordinal)
            || description.Contains("ruta anónima", StringComparison.Ordinal);
    }

    /// <summary>
    /// El rol se renderiza desde la política del endpoint, no se escribe a mano: si el
    /// transformer deja de aplicarse, esto falla aunque la prosa siga intacta. Se ancla en
    /// la constante de <see cref="AuthorizationPolicies"/>, así que renombrar el rol no
    /// rompe el test.
    /// </summary>
    [Theory]
    [InlineData("CreateEvent")]
    [InlineData("CancelEvent")]
    [InlineData("CreateRaffle")]
    [InlineData("DrawPrize")]
    public async Task ProtectedOperation_RendersItsRequiredRole_WhenTheDocumentIsServed(string operationId)
    {
        var expectedRole = operationId switch
        {
            "CreateEvent"  => AuthorizationPolicies.EventsWrite,
            "CancelEvent"  => AuthorizationPolicies.EventsCancel,
            "CreateRaffle" => AuthorizationPolicies.RafflesWrite,
            "DrawPrize"    => AuthorizationPolicies.RafflesDraw,
            _              => throw new ArgumentOutOfRangeException(nameof(operationId))
        };

        var document = await GetDocumentAsync();

        var operation = Operations(document).Single(o => o.Name == operationId);
        var description = operation.Element.GetProperty("description").GetString();

        description.ShouldNotBeNull();
        description.ShouldContain($"`{expectedRole}`");
    }

    /// <summary>
    /// La paginación la heredan los request de listado de un tipo base, y lo que hereda un
    /// record es exactamente lo que <c>[AsParameters]</c> puede dejar de publicar sin que
    /// ninguna respuesta cambie: el listado seguiría contestando, con la primera página siempre.
    /// </summary>
    [Theory]
    [InlineData("ListEvents")]
    [InlineData("ListMissingFiles")]
    public async Task EveryPagedOperation_DocumentsItsPagingParameters_WhenTheDocumentIsServed(string operationId)
    {
        var document = await GetDocumentAsync();

        var operation = Operations(document).Single(o => o.Name == operationId);
        // Sin distinguir mayúsculas: el documento publica el nombre de la propiedad tal cual y
        // el binder de la query no distingue, así que lo que importa es que el parámetro esté.
        var parameters = operation.Element.GetProperty("parameters")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        parameters.ShouldContain(n => string.Equals(n, "page", StringComparison.OrdinalIgnoreCase));
        parameters.ShouldContain(n => string.Equals(n, "pageSize", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Descarga y parsea el documento OpenAPI de la versión v1.
    /// </summary>
    private async Task<JsonDocument> GetDocumentAsync()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");

        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Aplana el documento a la lista de operaciones, identificadas por su operationId
    /// (o por "método ruta" si no lo llevara).
    /// </summary>
    private static List<(string Name, JsonElement Element)> Operations(JsonDocument document)
    {
        var operations = new List<(string, JsonElement)>();

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var method in HttpMethods)
            {
                if (!path.Value.TryGetProperty(method, out var operation))
                    continue;

                var name = operation.TryGetProperty("operationId", out var id) && id.GetString() is { } value
                    ? value
                    : $"{method.ToUpperInvariant()} {path.Name}";

                operations.Add((name, operation));
            }
        }

        return operations;
    }

    #endregion
}
