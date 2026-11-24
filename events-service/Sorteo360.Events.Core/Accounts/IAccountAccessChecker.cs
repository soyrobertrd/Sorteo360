namespace Sorteo360.Events.Core.Accounts;

/// <summary>
/// Comprueba si un <c>account_id</c> corresponde a una cuenta activa en la plataforma (R6).
/// </summary>
public interface IAccountAccessChecker
{
    Task<bool> IsActiveAccountAsync(Guid accountId, CancellationToken cancellationToken);
}

