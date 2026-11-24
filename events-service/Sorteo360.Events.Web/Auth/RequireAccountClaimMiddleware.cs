using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.Auth;

/// <summary>
/// Falla cerrado (403 <c>ACCOUNT_NOT_ACTIVE</c>) para peticiones autenticadas de usuarios sin
/// claim <c>account_id</c> válido o cuya cuenta no existe o no está activa (R6).
/// </summary>
public sealed class RequireAccountClaimMiddleware
{
    private readonly RequestDelegate _next;

    public RequireAccountClaimMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentAccount currentAccount,
        IAccountAccessChecker accessChecker)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Cuentas de servicio y rutas de platform-admin operan fuera del alcance de una cuenta cliente.
            var isServiceAccount = context.User.IsInRole(AuthorizationPolicies.PlatformService);
            var isPlatformAdminRoute = context.User.IsInRole(AuthorizationPolicies.PlatformAdmin)
                && context.Request.Path.StartsWithSegments("/v1/admin", StringComparison.OrdinalIgnoreCase);

            if (!isServiceAccount && !isPlatformAdminRoute)
            {
                var accountId = currentAccount.TryAccountId;
                if (accountId is null || !await accessChecker.IsActiveAccountAsync(accountId.Value, context.RequestAborted))
                {
                    await AccountErrors.AccountNotActive().ToProblem().ExecuteAsync(context);
                    return;
                }
            }
        }

        await _next(context);
    }
}
