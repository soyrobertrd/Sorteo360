using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Sorteo360.Events.Web.Auth;

namespace Sorteo360.Events.Web.OpenApi;

/// <summary>
/// Añade al final de la descripción de cada operación el rol funcional que exige,
/// leído de la política de autorización del endpoint (incluida la que aplica el
/// grupo completo). Nombre de política y nombre de rol son el mismo string (ver
/// <c>AuthorizationPolicies</c>), así que el rol nunca se escribe a mano en la
/// prosa: si se renombra en Keycloak y en las políticas, la documentación lo
/// sigue sola. Los endpoints sin política propia caen en la fallback policy, que
/// exige usuario autenticado con <c>platform:staff</c> o <c>platform:service</c>
/// (ADR-0025) y ningún rol funcional. Una ruta anónima, como el relevo de tokens del chasis,
/// lo dice en vez de heredar esa línea.
/// </summary>
public sealed class RequiredRoleOperationTransformer : IOpenApiOperationTransformer
{
    private const string RoleLabel = "**Rol requerido:**";
    private const string AuthenticatedOnly =
        $"**Acceso:** requiere usuario autenticado con `{AuthorizationPolicies.PlatformStaff}` o `{AuthorizationPolicies.PlatformService}`; no exige rol funcional.";
    private const string AnonymousOnly =
        "**Acceso:** ruta anónima; no lleva token.";

    /// <summary>
    /// Compone la línea de acceso y la anexa a la descripción de la operación.
    /// </summary>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var accessLine = IsAnonymous(context) ? AnonymousOnly : AccessLine(RequiredPolicies(context));
        operation.Description = Append(operation.Description, accessLine);

        return Task.CompletedTask;
    }

    /// <summary>Si el endpoint se publica sin autenticación (<c>AllowAnonymous</c>).</summary>
    private static bool IsAnonymous(OpenApiOperationTransformerContext context) =>
        context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();

    /// <summary>
    /// Devuelve los nombres de política exigidos por el endpoint, sin repetidos y en
    /// orden estable. Un endpoint sin política devuelve la lista vacía.
    /// </summary>
    private static IReadOnlyList<string> RequiredPolicies(OpenApiOperationTransformerContext context)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        return metadata
            .OfType<IAuthorizeData>()
            .Select(a => a.Policy)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Formatea la línea de acceso: los roles exigidos, o la nota de solo-autenticado
    /// cuando el endpoint no lleva política propia.
    /// </summary>
    private static string AccessLine(IReadOnlyList<string> policies)
    {
        if (policies.Count == 0)
            return AuthenticatedOnly;

        var roles = string.Join(", ", policies.Select(p => $"`{p}`"));

        return $"{RoleLabel} {roles}.";
    }

    /// <summary>
    /// Anexa la línea a la descripción existente separándola por un párrafo, o la
    /// usa como descripción completa si la operación solo traía summary.
    /// </summary>
    private static string Append(string? description, string accessLine)
    {
        return string.IsNullOrWhiteSpace(description)
            ? accessLine
            : $"{description.TrimEnd()}\n\n{accessLine}";
    }
}
