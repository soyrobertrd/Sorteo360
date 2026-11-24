using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.FeatureSwitches;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.FeatureSwitchEndpoints;

/// <summary>
/// Endpoints de interruptores de funciones (<c>/v1/feature-switches</c> y lectura pública
/// <c>/v1/public/feature-switches</c>), servidos desde <c>events-service</c> sin depender del CRM.
/// </summary>
public sealed class FeatureSwitchEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // El repositorio se registra en DomainServicesConfiguration.
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/feature-switches")
            .WithApiVersionSet(versionSet)
            .WithTags("Events")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("", ListFeatureSwitches)
            .MapToApiVersion(1)
            .WithName("ListFeatureSwitches")
            .WithSummary("Lista los interruptores de funciones configurados.")
            .WithDescription("Devuelve las filas explícitamente configuradas en el servidor. Toda clave ausente se considera habilitada.")
            .AllowAnonymous()
            .Produces<IReadOnlyList<FeatureSwitchHttpResponse>>(StatusCodes.Status200OK);

        group.MapPut("{key}", SetFeatureSwitch)
            .MapToApiVersion(1)
            .WithName("SetFeatureSwitch")
            .WithSummary("Habilita o deshabilita un interruptor de módulo o función.")
            .WithDescription("Guarda en PostgreSQL el estado del interruptor junto con el usuario que realizó el cambio.")
            .RequireAuthorization(AuthorizationPolicies.FeatureSwitchesWrite)
            .Produces<FeatureSwitchHttpResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var publicGroup = app.MapGroup("v{version:apiVersion}/public/feature-switches")
            .WithApiVersionSet(versionSet)
            .WithTags("Events")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        publicGroup.MapGet("", ListFeatureSwitches)
            .MapToApiVersion(1)
            .WithName("ListPublicFeatureSwitches")
            .WithSummary("Lectura anónima de interruptores de funciones para el portal público.")
            .WithDescription("Permite al portal público conocer si el módulo de convocatorias está habilitado sin requerir sesión.")
            .AllowAnonymous()
            .Produces<IReadOnlyList<FeatureSwitchHttpResponse>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> ListFeatureSwitches(
        [FromServices] IFeatureSwitchRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.ListAsync(cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        var items = result.Value.Select(FeatureSwitchHttpResponse.From).ToList();
        return Results.Ok(items);
    }

    private static async Task<IResult> SetFeatureSwitch(
        [FromRoute] string key,
        [FromBody] SetFeatureSwitchHttpRequest request,
        HttpContext httpContext,
        [FromServices] IFeatureSwitchRepository repository,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var normalizedKey = key?.Trim() ?? string.Empty;
        if (!FeatureSwitchSetting.IsValidKey(normalizedKey))
        {
            return new BaseError(
                "La clave del interruptor debe seguir el formato 'modulo' o 'modulo.funcion'.",
                "feature_switch.invalid_key",
                ErrorKind.Invalid).ToProblem();
        }

        var (actorId, actorName) = ResolveActor(httpContext.User);
        var next = new FeatureSwitchSetting(
            Key: normalizedKey,
            Enabled: request.Enabled,
            ChangedById: actorId,
            ChangedByName: actorName,
            ChangedAt: clock.GetUtcNow());

        var result = await repository.UpsertAsync(next, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(FeatureSwitchHttpResponse.From(result.Value));
    }

    private static (Guid? Id, string? Name) ResolveActor(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? id = Guid.TryParse(sub, out var parsed) ? parsed : null;

        var name = user.FindFirstValue("name")
            ?? user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue(ClaimTypes.Email);

        if (!string.IsNullOrWhiteSpace(name) && name.Length > 200)
            name = name[..200];

        return (id, string.IsNullOrWhiteSpace(name) ? null : name.Trim());
    }
}

public sealed record SetFeatureSwitchHttpRequest(bool Enabled);

public sealed record FeatureSwitchHttpResponse(
    string Key,
    bool Enabled,
    Guid? ChangedById,
    string? ChangedByName,
    DateTimeOffset? ChangedAt)
{
    public static FeatureSwitchHttpResponse From(FeatureSwitchSetting setting) =>
        new(setting.Key, setting.Enabled, setting.ChangedById, setting.ChangedByName, setting.ChangedAt);
}

