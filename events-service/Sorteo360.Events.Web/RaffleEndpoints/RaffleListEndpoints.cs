using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.GlobalEndpoints;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Las lecturas que cruzan sorteos: la lista de sorteos y las cifras de todos juntos. Planas y no
/// bajo un evento, porque son de muchos. Sin política propia, como toda lectura del sorteo (spec 00
/// §10).
/// </summary>
public sealed class RaffleListEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<ListRafflesRequest>, Validators.ListRafflesRequestValidator>();
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var raffles = app.MapGroup("v{version:apiVersion}/raffles")
            .WithApiVersionSet(versionSet)
            .WithTags("Raffles")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Sin policy: la fallback policy exige, eso sí, un usuario autenticado.
        raffles.MapGet("", ListRaffles)
            .MapToApiVersion(1)
            .WithName("ListRaffles")
            .WithSummary(RaffleEndpointsDescriptions.ListRafflesSummary)
            .WithDescription(RaffleEndpointsDescriptions.ListRafflesDescription)
            .WithValidation<ListRafflesRequest>()
            .Produces<PagedResponse<RaffleListItemResponse>>();

        raffles.MapGet("totals", GetTotals)
            .MapToApiVersion(1)
            .WithName("GetRaffleTotals")
            .WithSummary(RaffleEndpointsDescriptions.GetRaffleTotalsSummary)
            .WithDescription(RaffleEndpointsDescriptions.GetRaffleTotalsDescription)
            .Produces<RaffleTotalsResponse>();

    }

    private static async Task<IResult> ListRaffles(
        [AsParameters] ListRafflesRequest request,
        [FromServices] IRaffleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(request.ResolvedPage, request.ResolvedPageSize, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.Ok(result.Value.ToResponse(RaffleListItemResponse.From));
    }

    private static async Task<IResult> GetTotals(
        [FromServices] IRaffleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetTotalsAsync(cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.Ok(RaffleTotalsResponse.From(result.Value));
    }

}
