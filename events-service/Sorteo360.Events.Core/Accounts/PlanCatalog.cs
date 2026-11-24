namespace Sorteo360.Events.Core.Accounts;

/// <summary>
/// Catálogo fijo de planes de Sorteo360 (§9.2). No vive en base de datos; cada cuenta guarda la
/// clave de su plan vigente (<c>free</c>, <c>basic</c>, <c>pro</c>, <c>advanced</c>).
/// </summary>
public static class PlanCatalog
{
    public const string Free = "free";
    public const string Basic = "basic";
    public const string Pro = "pro";
    public const string Advanced = "advanced";

    public const string DefaultTimeZoneId = "America/Santo_Domingo";

    public static readonly IReadOnlyDictionary<string, PlanDefinition> ByKey =
        new Dictionary<string, PlanDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [Free] = new PlanDefinition(
                Key: Free,
                DisplayName: "Gratis",
                MaxParticipantsPerRaffle: 25,
                MaxActiveRaffles: null,
                MaxMonthlyRaffles: 1,
                MaxRoundsPerRaffle: 1,
                MaxPrizesPerRaffle: 10,
                MaxUsers: 1,
                ProjectorEnabled: false,
                ApiEnabled: false,
                CustomBrandingEnabled: false,
                MonthlyPriceDop: 0m),
            [Basic] = new PlanDefinition(
                Key: Basic,
                DisplayName: "Básico",
                MaxParticipantsPerRaffle: 250,
                MaxActiveRaffles: 3,
                MaxMonthlyRaffles: null,
                MaxRoundsPerRaffle: 5,
                MaxPrizesPerRaffle: 100,
                MaxUsers: 3,
                ProjectorEnabled: true,
                ApiEnabled: false,
                CustomBrandingEnabled: true,
                MonthlyPriceDop: 2900m),
            [Pro] = new PlanDefinition(
                Key: Pro,
                DisplayName: "Pro",
                MaxParticipantsPerRaffle: 1000,
                MaxActiveRaffles: 10,
                MaxMonthlyRaffles: null,
                MaxRoundsPerRaffle: 20,
                MaxPrizesPerRaffle: 1000,
                MaxUsers: 10,
                ProjectorEnabled: true,
                ApiEnabled: true,
                CustomBrandingEnabled: true,
                MonthlyPriceDop: 7900m),
            [Advanced] = new PlanDefinition(
                Key: Advanced,
                DisplayName: "Avanzado",
                MaxParticipantsPerRaffle: null,
                MaxActiveRaffles: null,
                MaxMonthlyRaffles: null,
                MaxRoundsPerRaffle: null,
                MaxPrizesPerRaffle: null,
                MaxUsers: null,
                ProjectorEnabled: true,
                ApiEnabled: true,
                CustomBrandingEnabled: true,
                MonthlyPriceDop: 19900m),
        };

    public static bool IsValidPlan(string? key) =>
        !string.IsNullOrWhiteSpace(key) && ByKey.ContainsKey(key.Trim());

    public static bool IsPaidPlan(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && ByKey.TryGetValue(key.Trim(), out var def)
        && !string.Equals(def.Key, Free, StringComparison.Ordinal);

    public static PlanDefinition GetOrFree(string? key) =>
        !string.IsNullOrWhiteSpace(key) && ByKey.TryGetValue(key.Trim(), out var def)
            ? def
            : ByKey[Free];

    /// <summary>Clave <c>yyyy-MM</c> del mes calendario actual en zona <c>America/Santo_Domingo</c>.</summary>
    public static string CurrentYearMonth(DateTimeOffset nowUtc)
    {
        var local = TimeZoneInfo.ConvertTime(nowUtc, ResolveSantoDomingoZone());
        return $"{local.Year:D4}-{local.Month:D2}";
    }

    /// <summary>Día 1 del próximo mes a las 00:00 en zona <c>America/Santo_Domingo</c>, expresado en UTC.</summary>
    public static DateTimeOffset NextMonthlyResetUtc(DateTimeOffset nowUtc)
    {
        var tz = ResolveSantoDomingoZone();
        var local = TimeZoneInfo.ConvertTime(nowUtc, tz);
        var nextMonthFirst = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1);
        var offset = tz.GetUtcOffset(nextMonthFirst);
        return new DateTimeOffset(nextMonthFirst, offset).ToUniversalTime();
    }

    private static TimeZoneInfo ResolveSantoDomingoZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("AST", TimeSpan.FromHours(-4), "Santo Domingo", "AST");
        }
    }
}

public sealed record PlanDefinition(
    string Key,
    string DisplayName,
    int? MaxParticipantsPerRaffle,
    int? MaxActiveRaffles,
    int? MaxMonthlyRaffles,
    int? MaxRoundsPerRaffle,
    int? MaxPrizesPerRaffle,
    int? MaxUsers,
    bool ProjectorEnabled,
    bool ApiEnabled,
    bool CustomBrandingEnabled,
    decimal MonthlyPriceDop);
