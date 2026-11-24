using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Core.Identity;

/// <summary>
/// Cuenta cliente a la que pertenece la petición actual (claim <c>account_id</c> del token).
/// Falla cerrado (R6): si no viene un claim válido, <see cref="TryAccountId"/> es <c>null</c>
/// y <see cref="AccountId"/> es <see cref="Guid.Empty"/>.
/// </summary>
public interface ICurrentAccount
{
    Guid AccountId { get; }
    Guid? TryAccountId => AccountId == Guid.Empty ? null : AccountId;
}

