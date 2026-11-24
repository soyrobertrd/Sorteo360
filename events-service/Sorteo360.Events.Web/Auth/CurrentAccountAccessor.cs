using System.Security.Claims;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Identity;

namespace Sorteo360.Events.Web.Auth;

/// <summary>
/// Resuelve <see cref="ICurrentAccount"/> desde el claim <c>account_id</c> del JWT actual.
/// </summary>
public sealed class CurrentAccountAccessor : ICurrentAccount
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentAccountAccessor(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TryAccountId
    {
        get
        {
            var raw = _httpContextAccessor.HttpContext?.User.FindFirstValue("account_id");
            return Guid.TryParse(raw, out var parsed) && parsed != Guid.Empty
                ? parsed
                : null;
        }
    }

    public Guid AccountId => TryAccountId ?? Guid.Empty;
}

