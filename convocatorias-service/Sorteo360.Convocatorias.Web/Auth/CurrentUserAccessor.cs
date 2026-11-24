using System.Security.Claims;
using Sorteo360.Convocatorias.Core.Identity;

namespace Sorteo360.Convocatorias.Web.Auth;

/// <summary>
/// Resuelve el usuario actual desde el principal de la petición: el claim <c>sub</c> del JWT
/// validado, o su equivalente mapeado. Registrado scoped; solo es usable dentro de una petición
/// autenticada, que es donde lo pide el lanzamiento.
/// <para>
/// Un token de credenciales de cliente también trae <c>sub</c>, así que una llamada de servicio
/// resuelve y se atribuye a la cuenta de servicio en vez de fallar.
/// </para>
/// </summary>
public sealed class CurrentUserAccessor : ICurrentUser
{
    private const string SubClaimName = "sub";
    private const string NameClaimName = "name";
    private const string UsernameClaimName = "preferred_username";
    private const string OptCodeClaimName = "opt_code";

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

    /// <inheritdoc />
    /// <remarks>La misma regla que el <c>CurrentUserAccessor</c> de crm-service.</remarks>
    public string? Name
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            var name = principal?.FindFirstValue(NameClaimName);

            if (!string.IsNullOrWhiteSpace(name))
                return name;

            var username = principal?.FindFirstValue(UsernameClaimName);
            return string.IsNullOrWhiteSpace(username) ? null : username;
        }
    }

    /// <inheritdoc />
    public string? OptCode
    {
        get
        {
            var code = _httpContextAccessor.HttpContext?.User.FindFirstValue(OptCodeClaimName);
            return string.IsNullOrWhiteSpace(code) ? null : code;
        }
    }
}
