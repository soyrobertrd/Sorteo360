using System.Text.RegularExpressions;

namespace Sorteo360.Events.Core.Branding;

/// <summary>
/// Configuración global de marca de la plataforma Sorteo360 persistida en el servidor.
/// </summary>
public sealed partial record GlobalBrandingSettings(
    string AppName,
    string AppSubtitle,
    string OrganizationName,
    string LogoUrl,
    string LogoDarkUrl,
    string PrimaryColor,
    string AccentColor,
    DateTimeOffset UpdatedAt)
{
    public const string DefaultAppName = "Sorteo360";
    public const string DefaultAppSubtitle = "Plataforma de Rifas y Check-in QR";
    public const string DefaultOrganizationName = "Empresa Anfitriona";
    public const string DefaultLogoUrl = "/logo-brand.svg";
    public const string DefaultLogoDarkUrl = "/logo-brand-dark.svg";
    public const string DefaultPrimaryColor = "#0d3b7d";
    public const string DefaultAccentColor = "#1ea2dc";

    public static GlobalBrandingSettings Defaults(DateTimeOffset updatedAt) => new(
        AppName: DefaultAppName,
        AppSubtitle: DefaultAppSubtitle,
        OrganizationName: DefaultOrganizationName,
        LogoUrl: DefaultLogoUrl,
        LogoDarkUrl: DefaultLogoDarkUrl,
        PrimaryColor: DefaultPrimaryColor,
        AccentColor: DefaultAccentColor,
        UpdatedAt: updatedAt);

    public static string NormalizeHexColor(string? color, string fallback)
    {
        if (string.IsNullOrWhiteSpace(color))
            return fallback;

        var trimmed = color.Trim();
        return HexColorRegex().IsMatch(trimmed) ? trimmed.ToLowerInvariant() : fallback;
    }

    public static string NormalizeText(string? value, string fallback, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}

/// <summary>
/// Marca personalizada para el sorteo de un evento concreto (nombre y logo del patrocinador o empresa).
/// </summary>
public sealed record EventBrandingSettings(
    Guid EventId,
    string? CompanyName,
    string? LogoUrl,
    DateTimeOffset UpdatedAt);

