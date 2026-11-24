using Asp.Versioning;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Identity;
using Sorteo360.Convocatorias.Core.Reconciliation;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Sorteo360.Convocatorias.Web.ReconciliationEndpoints;

/// <summary>
/// La pantalla de reconciliación (spec <c>07</c> §6): cinco conteos y un listado por cubo.
/// <para>
/// <b>Una ruta por cubo y no <c>/{bucket}</c> con un parámetro libre</b>: cada una declara en
/// OpenAPI la forma de su fila, y un cubo que no existe es un <c>404</c> del enrutado, sin un código
/// de error inventado para decirlo.
/// </para>
/// </summary>
public sealed class ReconciliationEndpoints : IEndpoint
{
    private const string ReconciliationRoute = $"{ApiRoutes.InternalPrefix}/reconciliation";

    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Ninguna ruta lleva cuerpo: no hay nada que validar.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var reconciliation = app.MapGroup(ReconciliationRoute)
            .WithApiVersionSet(versionSet)
            .WithTags("Reconciliation")
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        reconciliation.MapGet("", Summary)
            .MapToApiVersion(1)
            .WithName("GetReconciliationSummary")
            .WithSummary(ReconciliationEndpointsDescriptions.CountsSummary)
            .WithDescription(ReconciliationEndpointsDescriptions.CountsDescription)
            .Produces<ReconciliationCounts>(StatusCodes.Status200OK);

        reconciliation.MapGet("uncancelled-notices", UncancelledNotices)
            .MapToApiVersion(1)
            .WithName("ListUncancelledNotices")
            .WithSummary(ReconciliationEndpointsDescriptions.UncancelledNoticesSummary)
            .WithDescription(ReconciliationEndpointsDescriptions.UncancelledNoticesDescription)
            .Produces<PagedResponse<UncancelledNoticeRow>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        reconciliation.MapGet("pending-registration-sync", PendingRegistrationSync)
            .MapToApiVersion(1)
            .WithName("ListPendingRegistrationSync")
            .WithSummary(ReconciliationEndpointsDescriptions.PendingRegistrationSyncSummary)
            .Produces<PagedResponse<RegistrationReviewRow>>(StatusCodes.Status200OK);

        reconciliation.MapGet("details-differ", DetailsDiffer)
            .MapToApiVersion(1)
            .WithName("ListRegistrationDetailsDiffer")
            .WithSummary(ReconciliationEndpointsDescriptions.DetailsDifferSummary)
            .Produces<PagedResponse<RegistrationReviewRow>>(StatusCodes.Status200OK);

        reconciliation.MapGet("pending-unsubscribes", PendingUnsubscribes)
            .MapToApiVersion(1)
            .WithName("ListPendingUnsubscribes")
            .WithSummary(ReconciliationEndpointsDescriptions.PendingUnsubscribesSummary)
            .Produces<PagedResponse<PendingUnsubscribeRow>>(StatusCodes.Status200OK);

        reconciliation.MapGet("stranded-campaigns", StrandedCampaigns)
            .MapToApiVersion(1)
            .WithName("ListStrandedCampaigns")
            .WithSummary(ReconciliationEndpointsDescriptions.StrandedCampaignsSummary)
            .WithDescription(ReconciliationEndpointsDescriptions.StrandedCampaignsDescription)
            .Produces<PagedResponse<StrandedCampaignRow>>(StatusCodes.Status200OK);

        // Las exclusiones son una pregunta de la campaña y no de la reconciliación (07 §6.4), así
        // que cuelgan de su ruta y llevan su etiqueta; viven acá porque son otra lectura local más.
        app.MapGet($"{ApiRoutes.InternalPrefix}/campaigns/{{campaignId:guid}}/exclusions", Exclusions)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Campaigns")
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .WithName("ListCampaignExclusions")
            .WithSummary(ReconciliationEndpointsDescriptions.ExclusionsSummary)
            .WithDescription(ReconciliationEndpointsDescriptions.ExclusionsDescription)
            .Produces<PagedResponse<CampaignExclusionRow>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost($"{ApiRoutes.InternalPrefix}/registrations/{{id:guid}}/acknowledge-details", AcknowledgeDetails)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Reconciliation")
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .WithName("AcknowledgeRegistrationDetails")
            .WithSummary(ReconciliationEndpointsDescriptions.AcknowledgeDetailsSummary)
            .WithDescription(ReconciliationEndpointsDescriptions.AcknowledgeDetailsDescription)
            .Produces<RegistrationReviewRow>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> Summary(
        [FromServices] IReconciliationService reconciliation,
        CancellationToken cancellationToken)
    {
        var result = await reconciliation.CountAsync(cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.Ok(result.Value);
    }

    private static async Task<IResult> UncancelledNotices(
        [AsParameters] ReconciliationPageRequest request,
        [FromServices] IReconciliationService reconciliation,
        CancellationToken cancellationToken) =>
        Paged(await reconciliation.ListUncancelledNoticesAsync(request.PageNumber, request.Size, cancellationToken));

    private static async Task<IResult> PendingRegistrationSync(
        [AsParameters] ReconciliationPageRequest request,
        [FromServices] IReconciliationRepository repository,
        CancellationToken cancellationToken) =>
        Paged(await repository.ListPendingRegistrationSyncAsync(
            request.PageNumber, request.Size, cancellationToken));

    private static async Task<IResult> DetailsDiffer(
        [AsParameters] ReconciliationPageRequest request,
        [FromServices] IReconciliationRepository repository,
        CancellationToken cancellationToken) =>
        Paged(await repository.ListDetailsDifferAsync(request.PageNumber, request.Size, cancellationToken));

    private static async Task<IResult> PendingUnsubscribes(
        [AsParameters] ReconciliationPageRequest request,
        [FromServices] IReconciliationRepository repository,
        CancellationToken cancellationToken) =>
        Paged(await repository.ListPendingUnsubscribesAsync(request.PageNumber, request.Size, cancellationToken));

    private static async Task<IResult> StrandedCampaigns(
        [AsParameters] ReconciliationPageRequest request,
        [FromServices] IReconciliationRepository repository,
        CancellationToken cancellationToken) =>
        Paged(await repository.ListStrandedCampaignsAsync(request.PageNumber, request.Size, cancellationToken));

    private static async Task<IResult> Exclusions(
        [FromRoute] Guid campaignId,
        [AsParameters] ReconciliationPageRequest request,
        [FromServices] IReconciliationRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.ListExclusionsAsync(
            campaignId, request.PageNumber, request.Size, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.TryGetValue(out var page) ? Paged(page) : Results.NotFound();
    }

    private static async Task<IResult> AcknowledgeDetails(
        [FromRoute] Guid id,
        [FromServices] IReconciliationRepository repository,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await repository.AcknowledgeDetailsAsync(id, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.TryGetValue(out var row) ? Results.Ok(row) : Results.NotFound();
    }

    /// <summary>La página del Core en la forma paginada de la API, o el problema.</summary>
    private static IResult Paged<T>(Result<PagedResult<T>, BaseError> result) =>
        result.IsFailure ? result.Error.ToProblem() : Paged(result.Value);

    private static IResult Paged<T>(PagedResult<T> page) =>
        Results.Ok(new PagedResponse<T>
        {
            Items = page.Items,
            Total = page.Total,
            Page = page.Page,
            PageSize = page.PageSize,
        });
}

/// <summary>
/// Paginación de los cubos. Anulables y sin inicializador, por la misma razón que en
/// <see cref="ListCampaignsRequest"/>: con un <c>= 1</c> el enlace de <c>[AsParameters]</c> falla.
/// </summary>
public sealed record ReconciliationPageRequest
{
    /// <summary>Página, 1-based. Por omisión, la primera.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    /// <summary>Tamaño de página. Por omisión 20, y el servicio lo topa en 100.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }

    internal int PageNumber => Page ?? 1;

    internal int Size => PageSize ?? 20;
}
