using System.Security.Claims;
using Sorteo360.Events.Core.Identity;

namespace Sorteo360.Events.Web.Auth;

/// <summary>
/// Resuelve el usuario actual desde el principal de la petición HTTP: toma el
/// claim <c>sub</c> del JWT validado (o su equivalente mapeado). Registrado
/// scoped; solo es usable dentro de una petición autenticada.
/// </summary>
public sealed class CurrentUserAccessor : ICurrentUser
{
    private const string SubClaimName = "sub";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid Id
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            var sub = principal?.FindFirstValue(SubClaimName)
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(sub, out var id)
                ? id
                : throw new InvalidOperationException(
                    "La petición actual no tiene un usuario autenticado con claim 'sub' válido.");
        }
    }
}
