using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Entidad de persistencia para la tabla <c>events.branding_setting</c> (por cuenta o por evento).
/// </summary>
internal sealed class BrandingSettingData
{
    public string ScopeKey { get; set; } = string.Empty;
    public Guid AccountId { get; set; } = Account.DefaultBanreservasAccountId;
    public Guid? EventId { get; set; }
    public string? AppName { get; set; }
    public string? AppSubtitle { get; set; }
    public string? OrganizationName { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoDarkUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? AccentColor { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

