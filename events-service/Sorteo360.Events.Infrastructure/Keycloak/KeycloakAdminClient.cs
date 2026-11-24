using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Sorteo360.Events.Infrastructure.Keycloak;

/// <summary>
/// Cliente tipado de la Admin API de Keycloak. El <c>HttpClient</c> lo arma el chasis
/// (<c>AddServiceClient</c>): trae la URL base configurada, la firma con la identidad propia de
/// este servicio —la misma cuenta de servicio que lleva <c>view-realm</c> y <c>view-users</c>— y la
/// envuelve en la tubería de resiliencia. Acá solo viven las rutas, la paginación y la forma de las
/// respuestas.
/// </summary>
public sealed class KeycloakAdminClient : IKeycloakAdminClient
{
    private readonly HttpClient _httpClient;
    private readonly QuoteApproverSettings _settings;

    public KeycloakAdminClient(HttpClient httpClient, IOptions<QuoteApproverSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);

        _httpClient = httpClient;
        _settings = settings.Value;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<KeycloakRoleResponse>> ListRealmRolesAsync(CancellationToken cancellationToken) =>
        ReadAllPagesAsync<KeycloakRoleResponse>(
            $"{RealmRoute}/roles?briefRepresentation=true",
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<KeycloakRoleResponse>> GetRealmRoleCompositesAsync(
        string roleName,
        CancellationToken cancellationToken)
    {
        // Los hijos de un compuesto son pocos y el endpoint no pagina: se pide entero.
        var route = $"{RealmRoute}/roles/{Escape(roleName)}/composites/realm";

        using var response = await _httpClient.GetAsync(route, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<KeycloakRoleResponse>>(cancellationToken) ?? [];
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<KeycloakUserResponse>> GetRealmRoleMembersAsync(
        string roleName,
        CancellationToken cancellationToken) =>
        ReadAllPagesAsync<KeycloakUserResponse>(
            $"{RealmRoute}/roles/{Escape(roleName)}/users?briefRepresentation=false",
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<KeycloakUserResponse>> FindUsersByAttributeAsync(
        string attribute,
        string value,
        CancellationToken cancellationToken) =>
        ReadAllPagesAsync<KeycloakUserResponse>(
            $"{RealmRoute}/users?q={Escape($"{attribute}:{value}")}",
            cancellationToken);

    /// <inheritdoc />
    public async Task<KeycloakUserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        using var response = await _httpClient.GetAsync(
            $"{RealmRoute}/users/{Escape(userId)}", cancellationToken);

        // El 404 es la respuesta a "esta cuenta ya no está", no una avería del realm, así que se
        // traduce a null antes de EnsureSuccessStatusCode. Confundir las dos cosas haría que una
        // coordinadora que dejó el MITUR se registrara como un envío fallido en vez de como lo que
        // es: no hay a quién escribirle.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<KeycloakUserResponse>(cancellationToken);
    }

    /// <summary>La raíz de la Admin API para el realm configurado.</summary>
    private string RealmRoute => $"admin/realms/{Escape(_settings.Realm)}";

    /// <summary>
    /// Escapa un segmento o un valor de consulta. No es adorno: el rol se llama
    /// <c>quotes:approve</c>, y los dos puntos sin escapar cambian lo que Keycloak entiende.
    /// </summary>
    private static string Escape(string value) => Uri.EscapeDataString(value);

    /// <summary>
    /// Recorre todas las páginas de un listado y devuelve el conjunto completo.
    /// <para>
    /// Los dos endpoints paginados devuelven 100 por defecto, ordenados por usuario ascendente, y
    /// no existe ninguno que diga cuántos hay: pedir sin paginar devolvería los primeros cien en
    /// silencio, que es exactamente la respuesta equivocada que parece correcta. Se para cuando una
    /// página viene incompleta, que es lo que marca el final sin un total.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<T>> ReadAllPagesAsync<T>(string route, CancellationToken cancellationToken)
    {
        var separator = route.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var pageSize = _settings.PageSize;
        var all = new List<T>();

        for (var first = 0; ; first += pageSize)
        {
            using var response = await _httpClient.GetAsync(
                $"{route}{separator}first={first}&max={pageSize}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var page = await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken) ?? [];
            all.AddRange(page);

            if (page.Count < pageSize)
                return all;
        }
    }
}
