using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.AccountEndpoints;

/// <summary>
/// Registro anónimo de cuentas, activación con contraseña directa a Keycloak y consulta del
/// resumen de cuenta/plan/uso (§9.3, §9.7 A25 y A27).
/// <para>
/// Las rutas públicas de alta viven bajo <c>/v1/accounts/*</c> (y no <c>/v1/public/*</c>) porque
/// el proxy enruta <c>/v1/public/</c> a <c>convocatorias-service</c>.
/// </para>
/// </summary>
public sealed class AccountEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        MapCurrentAccount(app, versionSet);

        // El registro da account_admin y ocultar la página del SPA no basta (S1.1, auditoría 2026-11):
        // apagado, todo POST anónimo bajo /accounts responde 404. Sin esta ruta la política de
        // respaldo respondería 401, que no deja claro que la ruta no existe.
        if (app.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>("Accounts:SignupEnabled"))
            MapPublicSignup(app, versionSet);
        else
            app.MapGroup("v{version:apiVersion}/accounts")
                .WithApiVersionSet(versionSet)
                .AllowAnonymous()
                .ExcludeFromDescription()
                .MapPost("{**path}", () => Results.NotFound())
                .MapToApiVersion(1);
    }

    private static void MapPublicSignup(IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        var publicGroup = app.MapGroup("v{version:apiVersion}/accounts")
            .WithApiVersionSet(versionSet)
            .WithTags("Accounts")
            .AllowAnonymous()
            .RequireRateLimiting("accounts-public")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        publicGroup.MapPost("signup", Signup)
            .MapToApiVersion(1)
            .WithName("SignupAccount")
            .WithSummary("Registra una cuenta nueva pendiente de activación y envía el enlace por correo.")
            .Produces<SignupHttpResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        publicGroup.MapPost("signup/resend", ResendActivation)
            .MapToApiVersion(1)
            .WithName("ResendAccountActivation")
            .WithSummary("Reenvía el correo de activación (máximo 1 por minuto y 5 por día).")
            .Produces<SignupHttpResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        publicGroup.MapPost("activate", ActivateAccount)
            .MapToApiVersion(1)
            .WithName("ActivateAccount")
            .WithSummary("Activa la cuenta creando el usuario administrador en Keycloak con la contraseña elegida.")
            .Produces<ActivateAccountHttpResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static void MapCurrentAccount(IEndpointRouteBuilder app, ApiVersionSet versionSet)
    {
        var currentGroup = app.MapGroup("v{version:apiVersion}/account")
            .WithApiVersionSet(versionSet)
            .WithTags("Accounts")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        currentGroup.MapGet("", GetCurrentAccount)
            .MapToApiVersion(1)
            .WithName("GetCurrentAccount")
            .WithSummary("Devuelve el plan vigente, el uso actual, los topes, la fecha de reinicio y las funciones habilitadas.")
            .Produces<AccountSummaryHttpResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Signup(
        [FromBody] SignupHttpRequest request,
        [FromServices] IAccountOnboardingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SignupAsync(new SignupInput(
            request.CompanyName ?? string.Empty,
            request.FullName ?? string.Empty,
            request.Email ?? string.Empty,
            request.Plan ?? PlanCatalog.Free,
            request.BillingPeriod ?? "monthly",
            request.AcceptTerms), cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(new SignupHttpResponse(DemoActivationPath: null));
    }

    private static async Task<IResult> ResendActivation(
        [FromBody] ResendActivationHttpRequest request,
        [FromServices] IAccountOnboardingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ResendActivationAsync(request.Email, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(new SignupHttpResponse(DemoActivationPath: null));
    }

    private static async Task<IResult> ActivateAccount(
        [FromBody] ActivateAccountHttpRequest request,
        [FromServices] IAccountOnboardingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ActivateAsync(request.Token, request.Password, cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code == "ACTIVATION_TOKEN_INVALID")
            {
                return Results.Problem(
                    title: "Enlace de activación inválido",
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status410Gone,
                    extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
            }

            if (result.Error.Code == "PASSWORD_POLICY_REJECTED")
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["password"] = [result.Error.Message] },
                    title: "Contraseña no válida",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
            }

            return result.Error.ToProblem();
        }

        return Results.Ok(new ActivateAccountHttpResponse(
            result.Value.Email,
            result.Value.CompanyName,
            result.Value.RequestedPlan));
    }

    private static async Task<IResult> GetCurrentAccount(
        [FromServices] ICurrentAccount currentAccount,
        [FromServices] IAccountOnboardingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAccountSummaryAsync(currentAccount.AccountId, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        var s = result.Value;
        return Results.Ok(new AccountSummaryHttpResponse(
            Id: s.Account.Id,
            Name: s.Account.Name,
            Plan: s.Plan.Key,
            PlanName: s.Plan.DisplayName,
            Status: s.Account.Status,
            PlanStartedAt: s.Account.PlanStartedAt,
            PlanExpiresAt: s.Account.PlanExpiresAt,
            Usage: new AccountUsageHttpResponse(s.ActiveRaffles, s.MonthlyRafflesCreated, s.UsersCount),
            Limits: new AccountLimitsHttpResponse(
                s.Plan.MaxParticipantsPerRaffle,
                s.Plan.MaxActiveRaffles,
                s.Plan.MaxMonthlyRaffles,
                s.Plan.MaxRoundsPerRaffle,
                s.Plan.MaxPrizesPerRaffle,
                s.Plan.MaxUsers),
            ResetsAt: s.ResetsAt,
            Features: new AccountFeaturesHttpResponse(
                s.Plan.ProjectorEnabled,
                s.Plan.ApiEnabled,
                s.Plan.CustomBrandingEnabled)));
    }
}

public sealed record SignupHttpRequest(
    string? CompanyName,
    string? FullName,
    string? Email,
    string? Plan,
    string? BillingPeriod,
    bool AcceptTerms);

public sealed record SignupHttpResponse(string? DemoActivationPath = null);

public sealed record ResendActivationHttpRequest(string? Email);

public sealed record ActivateAccountHttpRequest(string? Token, string? Password);

public sealed record ActivateAccountHttpResponse(
    string Email,
    string CompanyName,
    string? RequestedPlan);

public sealed record AccountUsageHttpResponse(
    int ActiveRaffles,
    int MonthlyRafflesCreated,
    int Users);

public sealed record AccountLimitsHttpResponse(
    int? MaxParticipantsPerRaffle,
    int? MaxActiveRaffles,
    int? MaxMonthlyRaffles,
    int? MaxRoundsPerRaffle,
    int? MaxPrizesPerRaffle,
    int? MaxUsers);

public sealed record AccountFeaturesHttpResponse(
    bool Projector,
    bool Api,
    bool Branding);

public sealed record AccountSummaryHttpResponse(
    Guid Id,
    string Name,
    string Plan,
    string PlanName,
    string Status,
    DateTimeOffset? PlanStartedAt,
    DateTimeOffset? PlanExpiresAt,
    AccountUsageHttpResponse Usage,
    AccountLimitsHttpResponse Limits,
    DateTimeOffset ResetsAt,
    AccountFeaturesHttpResponse Features);

