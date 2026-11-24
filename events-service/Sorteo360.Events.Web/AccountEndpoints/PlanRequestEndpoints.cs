using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Infrastructure.Notifications;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.AccountEndpoints;

/// <summary>
/// Endpoints de solicitudes de plan por transferencia bancaria (§9.5):
/// - Cuenta: pedir plan, ver datos bancarios y solicitudes, cancelar solicitud pendiente.
/// - Superadministrador (<c>platform-admin</c>): listar, aprobar (subiendo plan en la misma transacción) y rechazar.
/// </summary>
public sealed class PlanRequestEndpoints : IEndpoint
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

        var accountGroup = app.MapGroup("v{version:apiVersion}/account/plan-requests")
            .WithApiVersionSet(versionSet)
            .WithTags("Account Plans")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        accountGroup.MapGet("", ListAccountPlanRequests)
            .MapToApiVersion(1)
            .WithName("ListAccountPlanRequests")
            .WithSummary("Lista las solicitudes de plan de la cuenta actual y los datos para transferencia.")
            .Produces<AccountPlanRequestsResponse>(StatusCodes.Status200OK);

        accountGroup.MapPost("", CreateAccountPlanRequest)
            .MapToApiVersion(1)
            .WithName("CreateAccountPlanRequest")
            .WithSummary("Abre una solicitud de plan de pago por transferencia.")
            .Produces<PlanRequestResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        accountGroup.MapPost("{id:guid}/cancel", CancelAccountPlanRequest)
            .MapToApiVersion(1)
            .WithName("CancelAccountPlanRequest")
            .WithSummary("Cancela una solicitud de plan pendiente de la cuenta actual.")
            .Produces<PlanRequestResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var adminGroup = app.MapGroup("v{version:apiVersion}/admin/plan-requests")
            .WithApiVersionSet(versionSet)
            .WithTags("Platform Admin Plans")
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        adminGroup.MapGet("", ListAdminPlanRequests)
            .MapToApiVersion(1)
            .WithName("ListAdminPlanRequests")
            .WithSummary("Lista todas las solicitudes de plan (opcionalmente filtradas por estado).")
            .Produces<IReadOnlyList<PlanRequestResponse>>(StatusCodes.Status200OK);

        adminGroup.MapPost("{id:guid}/approve", ApprovePlanRequest)
            .MapToApiVersion(1)
            .WithName("ApprovePlanRequest")
            .WithSummary("Aprueba una solicitud pendiente y activa el plan pagado en la misma transacción.")
            .Produces<PlanRequestResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        adminGroup.MapPost("{id:guid}/reject", RejectPlanRequest)
            .MapToApiVersion(1)
            .WithName("RejectPlanRequest")
            .WithSummary("Rechaza una solicitud pendiente indicando el motivo.")
            .Produces<PlanRequestResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ListAccountPlanRequests(
        HttpContext httpContext,
        [FromServices] IPlanRequestService service,
        [FromServices] IOptions<PlanBillingSettings> billing,
        CancellationToken cancellationToken)
    {
        var accountId = ResolveAccountId(httpContext.User);
        var result = await service.ListForAccountAsync(accountId, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        var (account, requests) = result.Value;
        var b = billing.Value;

        return Results.Ok(new AccountPlanRequestsResponse(
            AccountId: account.Id,
            AccountName: account.Name,
            CurrentPlan: account.Plan,
            PlanExpiresAt: account.PlanExpiresAt,
            BankTransfer: new BankTransferInfoResponse(
                b.BankName, b.AccountHolder, b.AccountNumber, b.AccountType, b.TaxId, b.Instructions),
            Requests: requests.Select(PlanRequestResponse.From).ToList()));
    }

    private static async Task<IResult> CreateAccountPlanRequest(
        HttpContext httpContext,
        [FromBody] CreatePlanRequestHttpRequest request,
        [FromServices] IPlanRequestService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var accountId = ResolveAccountId(httpContext.User);
        var requesterEmail = httpContext.User.FindFirstValue(ClaimTypes.Email)
            ?? httpContext.User.FindFirstValue("email");

        var result = await service.CreateAsync(
            accountId,
            request.Plan,
            request.TransferReference,
            currentUser.Id,
            requesterEmail,
            cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        return Results.Created(
            $"/v{version}/account/plan-requests/{result.Value.Id}",
            PlanRequestResponse.From(result.Value));
    }

    private static async Task<IResult> CancelAccountPlanRequest(
        HttpContext httpContext,
        [FromRoute] Guid id,
        [FromServices] IPlanRequestService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var accountId = ResolveAccountId(httpContext.User);
        var result = await service.CancelAsync(accountId, id, currentUser.Id, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(PlanRequestResponse.From(result.Value));
    }

    private static async Task<IResult> ListAdminPlanRequests(
        [FromQuery] string? status,
        [FromServices] IPlanRequestService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAllAsync(status, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(result.Value.Select(PlanRequestResponse.From).ToList());
    }

    private static async Task<IResult> ApprovePlanRequest(
        [FromRoute] Guid id,
        [FromBody] ApprovePlanRequestHttpRequest? request,
        [FromServices] IPlanRequestService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveAsync(
            id,
            currentUser.Id,
            request?.PaidPeriodStart,
            request?.PaidPeriodEnd,
            cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(PlanRequestResponse.From(result.Value));
    }

    private static async Task<IResult> RejectPlanRequest(
        [FromRoute] Guid id,
        [FromBody] RejectPlanRequestHttpRequest request,
        [FromServices] IPlanRequestService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectAsync(id, currentUser.Id, request.Reason, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(PlanRequestResponse.From(result.Value));
    }

    private static Guid ResolveAccountId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("account_id");
        return Guid.TryParse(raw, out var parsed) && parsed != Guid.Empty
            ? parsed
            : Account.DefaultBanreservasAccountId;
    }
}

public sealed record CreatePlanRequestHttpRequest(string? Plan, string? TransferReference = null);

public sealed record ApprovePlanRequestHttpRequest(
    DateTimeOffset? PaidPeriodStart = null,
    DateTimeOffset? PaidPeriodEnd = null);

public sealed record RejectPlanRequestHttpRequest(string? Reason);

public sealed record BankTransferInfoResponse(
    string BankName,
    string AccountHolder,
    string AccountNumber,
    string AccountType,
    string? TaxId,
    string? Instructions);

public sealed record AccountPlanRequestsResponse(
    Guid AccountId,
    string AccountName,
    string CurrentPlan,
    DateTimeOffset? PlanExpiresAt,
    BankTransferInfoResponse BankTransfer,
    IReadOnlyList<PlanRequestResponse> Requests);

public sealed record PlanRequestResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string RequestedPlan,
    string RequestedPlanName,
    decimal MonthlyPriceDop,
    string Status,
    string? TransferReference,
    DateTimeOffset RequestedAt,
    Guid RequestedById,
    string? RequesterEmail,
    DateTimeOffset? DecidedAt,
    Guid? DecidedById,
    string? RejectionReason,
    DateTimeOffset? PaidPeriodStart,
    DateTimeOffset? PaidPeriodEnd)
{
    public static PlanRequestResponse From(PlanRequest r)
    {
        var plan = PlanCatalog.GetOrFree(r.RequestedPlan);
        return new PlanRequestResponse(
            r.Id,
            r.AccountId,
            r.AccountName,
            r.RequestedPlan,
            plan.DisplayName,
            plan.MonthlyPriceDop,
            r.Status,
            r.TransferReference,
            r.RequestedAt,
            r.RequestedById,
            r.RequesterEmail,
            r.DecidedAt,
            r.DecidedById,
            r.RejectionReason,
            r.PaidPeriodStart,
            r.PaidPeriodEnd);
    }
}

