namespace Sorteo360.Platform.Http;

/// <summary>
/// Fuente del token con el que este servicio se identifica ante otro servicio de la
/// plataforma. Lo consume el <see cref="ClientCredentialsTokenHandler"/>; ningún cliente
/// tipado debería llamarlo directamente.
/// </summary>
public interface IServiceTokenProvider
{
    /// <summary>
    /// Devuelve un token vigente, reutilizando el que tenga en caché mientras le quede
    /// margen de vida.
    /// </summary>
    /// <param name="forceRefresh">
    /// Descartar el token en caché y pedir uno nuevo. Es lo que hace el handler cuando el
    /// destino responde 401: el token pudo quedar invalidado antes de su caducidad (rotación
    /// de claves del realm, sesión revocada), y reintentar con el mismo daría otro 401.
    /// </param>
    /// <param name="cancellationToken">Cancelación de la petición saliente en curso.</param>
    /// <exception cref="ServiceAuthenticationException">
    /// No se pudo obtener un token del emisor.
    /// </exception>
    Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken);
}
