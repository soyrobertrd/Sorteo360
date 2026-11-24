using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Infrastructure.Keycloak;

/// <summary>
/// Lee del realm, por id y en vivo, el nombre de cada operador que nombra el acta.
/// <para>
/// <b>Sin caché.</b> Se pregunta una vez por sorteo, al completarlo, y el acta congela lo que
/// conteste: una copia no ahorraría nada que valga la pena y sí podría escribir en un documento
/// legal un nombre viejo.
/// </para>
/// <para>
/// <b>No falla nunca.</b> Una cuenta que ya no está y un realm que no contesta dan lo mismo, un
/// nulo, porque para el acta son el mismo hecho: no se sabe el nombre. Lo que cambia es qué tan
/// fuerte se registra.
/// </para>
/// </summary>
public sealed class KeycloakRaffleOperatorDirectory : IRaffleOperatorDirectory
{
    private readonly IKeycloakAdminClient _client;
    private readonly ILogger<KeycloakRaffleOperatorDirectory> _logger;

    public KeycloakRaffleOperatorDirectory(
        IKeycloakAdminClient client,
        ILogger<KeycloakRaffleOperatorDirectory> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string?>> ResolveNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var names = new Dictionary<Guid, string?>();
        foreach (var userId in userIds.Distinct())
            names[userId] = await ResolveNameAsync(userId, cancellationToken);

        return names;
    }

    private async Task<string?> ResolveNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _client.GetUserAsync(userId.ToString(), cancellationToken);
            if (user is null)
                _logger.LogWarning(
                    "El realm no tiene al operador {UserId}; el acta lo nombra como usuario no disponible.",
                    userId);

            return user?.DisplayName();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "No se pudo leer en Keycloak al operador {UserId}; el acta lo nombra como usuario no disponible.",
                userId);
            return null;
        }
    }
}
