using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Branding;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.BrandingEndpoints;

/// <summary>
/// Endpoints de configuración de marca en servidor (<c>/v1/branding</c> y marca por evento).
/// </summary>
public sealed class BrandingEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // El repositorio de marca se registra en DomainServicesConfiguration.
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/branding")
            .WithApiVersionSet(versionSet)
            .WithTags("Events")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("", GetBranding)
            .MapToApiVersion(1)
            .WithName("GetBranding")
            .WithSummary("Obtiene la configuración global de marca y las personalizaciones por evento.")
            .WithDescription("Devuelve el nombre de la aplicación, organización, URLs de logos y colores corporativos persistidos en PostgreSQL.")
            .AllowAnonymous()
            .Produces<BrandingHttpResponse>(StatusCodes.Status200OK);

        group.MapPut("", UpdateBranding)
            .MapToApiVersion(1)
            .WithName("UpdateBranding")
            .WithSummary("Actualiza la configuración global de marca.")
            .WithDescription("Guarda en el servidor el nombre visible, organización, logos y colores corporativos de Sorteo360 (acepta cuerpo plano o anidado en `global`).")
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces<BrandingHttpResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var eventGroup = app.MapGroup("v{version:apiVersion}/events/{eventId:guid}/raffle/branding")
            .WithApiVersionSet(versionSet)
            .WithTags("Events")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        eventGroup.MapGet("", GetEventBranding)
            .MapToApiVersion(1)
            .WithName("GetEventRaffleBranding")
            .WithSummary("Obtiene la marca personalizada del sorteo de un evento.")
            .WithDescription("Devuelve el nombre comercial y logo propios del evento si fueron configurados; si no, propiedades nulas.")
            .Produces<EventBrandingDto>(StatusCodes.Status200OK);

        eventGroup.MapPut("", UpdateEventBranding)
            .MapToApiVersion(1)
            .WithName("UpdateEventRaffleBranding")
            .WithSummary("Actualiza la marca personalizada del sorteo de un evento.")
            .WithDescription("Guarda o limpia el nombre comercial y logo propios del evento sin alterar la configuración global.")
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces<EventBrandingDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetBranding(
        HttpContext httpContext,
        [FromQuery] Guid? eventId,
        [FromServices] IBrandingRepository repository,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // S5: en peticiones anónimas, resolver la cuenta por eventId si viene; sin eventId
        // devolver siempre la marca neutra de Sorteo360 (nunca filtrar la marca de otra cuenta).
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            if (eventId is { } id && id != Guid.Empty)
            {
                var pubResult = await repository.GetPublicForEventAsync(id, cancellationToken);
                if (pubResult.IsFailure)
                    return pubResult.Error.ToProblem();

                var byEvent = new Dictionary<Guid, EventBrandingSettings>();
                if (pubResult.Value.EventBranding is { } eb)
                    byEvent[id] = eb;

                return Results.Ok(BrandingHttpResponse.From(pubResult.Value.Global, byEvent));
            }

            return Results.Ok(BrandingHttpResponse.From(
                GlobalBrandingSettings.Defaults(clock.GetUtcNow()),
                new Dictionary<Guid, EventBrandingSettings>()));
        }

        var globalResult = await repository.GetGlobalAsync(cancellationToken);
        if (globalResult.IsFailure)
            return globalResult.Error.ToProblem();

        var eventsResult = await repository.ListEventBrandingsAsync(cancellationToken);
        if (eventsResult.IsFailure)
            return eventsResult.Error.ToProblem();

        return Results.Ok(BrandingHttpResponse.From(globalResult.Value, eventsResult.Value));
    }

    private static async Task<IResult> UpdateBranding(
        [FromBody] UpdateBrandingHttpRequest request,
        [FromServices] IBrandingRepository repository,
        [FromServices] ICurrentAccount currentAccount,
        [FromServices] IPlanLimitGuard planLimits,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var featureCheck = await planLimits.EnsureFeatureEnabledAsync(currentAccount.AccountId, "branding", cancellationToken);
        if (featureCheck.IsFailure)
            return featureCheck.Error.ToProblem();

        var currentResult = await repository.GetGlobalAsync(cancellationToken);
        if (currentResult.IsFailure)
            return currentResult.Error.ToProblem();

        var current = currentResult.Value;
        var source = request.Global ?? request;
        var now = clock.GetUtcNow();

        var nextGlobal = new GlobalBrandingSettings(
            AppName: GlobalBrandingSettings.NormalizeText(source.AppName, current.AppName, 120),
            AppSubtitle: GlobalBrandingSettings.NormalizeText(source.AppSubtitle, current.AppSubtitle, 200),
            OrganizationName: GlobalBrandingSettings.NormalizeText(source.OrganizationName, current.OrganizationName, 200),
            LogoUrl: GlobalBrandingSettings.NormalizeText(source.LogoUrl, current.LogoUrl, 2000),
            LogoDarkUrl: GlobalBrandingSettings.NormalizeText(source.LogoDarkUrl, current.LogoDarkUrl, 2000),
            PrimaryColor: GlobalBrandingSettings.NormalizeHexColor(source.PrimaryColor, current.PrimaryColor),
            AccentColor: GlobalBrandingSettings.NormalizeHexColor(source.AccentColor, current.AccentColor),
            UpdatedAt: now);

        var saveGlobal = await repository.UpsertGlobalAsync(nextGlobal, cancellationToken);
        if (saveGlobal.IsFailure)
            return saveGlobal.Error.ToProblem();

        if (request.ByEvent is not null)
        {
            foreach (var (rawEventId, eventBranding) in request.ByEvent)
            {
                if (!Guid.TryParse(rawEventId, out var parsedEventId) || parsedEventId == Guid.Empty)
                    continue;

                var saveEvent = await repository.UpsertEventBrandingAsync(
                    new EventBrandingSettings(
                        parsedEventId,
                        TrimLimit(eventBranding.CompanyName, 200),
                        TrimLimit(eventBranding.LogoUrl, 2000),
                        now),
                    cancellationToken);

                // Un id que no es un evento de esta cuenta se salta, igual que uno mal escrito:
                // el mapa viene del navegador y puede traer eventos ya borrados.
                if (saveEvent.IsFailure && saveEvent.Error.Code != EventImageErrors.EventNotFound().Code)
                    return saveEvent.Error.ToProblem();
            }
        }

        var eventsResult = await repository.ListEventBrandingsAsync(cancellationToken);
        if (eventsResult.IsFailure)
            return eventsResult.Error.ToProblem();

        return Results.Ok(BrandingHttpResponse.From(saveGlobal.Value, eventsResult.Value));
    }

    private static async Task<IResult> GetEventBranding(
        [FromRoute] Guid eventId,
        [FromServices] IBrandingRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetEventBrandingAsync(eventId, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(new EventBrandingDto(result.Value?.CompanyName, result.Value?.LogoUrl));
    }

    private static async Task<IResult> UpdateEventBranding(
        [FromRoute] Guid eventId,
        [FromBody] EventBrandingDto request,
        [FromServices] IBrandingRepository repository,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var settings = new EventBrandingSettings(
            eventId,
            TrimLimit(request.CompanyName, 200),
            TrimLimit(request.LogoUrl, 2000),
            clock.GetUtcNow());

        var result = await repository.UpsertEventBrandingAsync(settings, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(new EventBrandingDto(settings.CompanyName, settings.LogoUrl));
    }

    private static string? TrimLimit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed record GlobalBrandingDto(
    string AppName,
    string AppSubtitle,
    string OrganizationName,
    string LogoUrl,
    string LogoDarkUrl,
    string PrimaryColor,
    string AccentColor);

public sealed record EventBrandingDto(string? CompanyName = null, string? LogoUrl = null);

public sealed class UpdateBrandingHttpRequest
{
    public string? AppName { get; init; }
    public string? AppSubtitle { get; init; }
    public string? OrganizationName { get; init; }
    public string? LogoUrl { get; init; }
    public string? LogoDarkUrl { get; init; }
    public string? PrimaryColor { get; init; }
    public string? AccentColor { get; init; }
    public UpdateBrandingHttpRequest? Global { get; init; }
    public Dictionary<string, EventBrandingDto>? ByEvent { get; init; }
}

public sealed class BrandingHttpResponse
{
    public required string AppName { get; init; }
    public required string AppSubtitle { get; init; }
    public required string OrganizationName { get; init; }
    public required string LogoUrl { get; init; }
    public required string LogoDarkUrl { get; init; }
    public required string PrimaryColor { get; init; }
    public required string AccentColor { get; init; }
    public required GlobalBrandingDto Global { get; init; }
    public required Dictionary<string, EventBrandingDto> ByEvent { get; init; }

    public static BrandingHttpResponse From(
        GlobalBrandingSettings global,
        IReadOnlyDictionary<Guid, EventBrandingSettings> byEvent)
    {
        var globalDto = new GlobalBrandingDto(
            global.AppName,
            global.AppSubtitle,
            global.OrganizationName,
            global.LogoUrl,
            global.LogoDarkUrl,
            global.PrimaryColor,
            global.AccentColor);

        var eventMap = byEvent.ToDictionary(
            kvp => kvp.Key.ToString("D"),
            kvp => new EventBrandingDto(kvp.Value.CompanyName, kvp.Value.LogoUrl));

        return new BrandingHttpResponse
        {
            AppName = global.AppName,
            AppSubtitle = global.AppSubtitle,
            OrganizationName = global.OrganizationName,
            LogoUrl = global.LogoUrl,
            LogoDarkUrl = global.LogoDarkUrl,
            PrimaryColor = global.PrimaryColor,
            AccentColor = global.AccentColor,
            Global = globalDto,
            ByEvent = eventMap,
        };
    }
}
