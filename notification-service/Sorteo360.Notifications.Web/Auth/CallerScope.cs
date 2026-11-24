using System.Security.Claims;
using Sorteo360.Notifications.Core.History;

namespace Sorteo360.Notifications.Web.Auth;

/// <summary>
/// De un token al alcance de lectura que le toca.
/// <para>
/// Lo único que mira es si quien llama es una cuenta de servicio, y eso lo dice el nombre de
/// usuario: Keycloak nombra al usuario de una cuenta de servicio
/// <c>service-account-&lt;clientId&gt;</c>, y una cuenta de servicio ES un usuario, con sus roles
/// colgando igual que los de una persona. Ningún rol compuesto aparece acá: quién es
/// <c>central_admin</c> lo decide el realm, y lo único que este código necesita saber es si del
/// otro lado hay una máquina.
/// </para>
/// </summary>
public static class CallerScope
{
    /// <summary>Claim con el que Keycloak manda el nombre de usuario (<c>MapInboundClaims = false</c>).</summary>
    private const string PreferredUsernameClaim = "preferred_username";

    /// <summary>El alcance de lectura de quien manda esta petición.</summary>
    /// <param name="user">El principal de la petición.</param>
    public static HistoryScope Of(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return HistoryScope.ForCaller(user.FindFirstValue(PreferredUsernameClaim));
    }
}
