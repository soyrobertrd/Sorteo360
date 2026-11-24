using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// La búsqueda de un sorteo de premio por su código de retiro. Plana y no bajo el evento: quien busca
/// no sabe de qué evento viene el código, y por eso existe la ruta (spec 03 §2.4). Solo lee: entregar
/// es siempre <c>POST …/handover</c>, con quien retira y su firma.
/// </summary>
public sealed class PrizeDrawLookupEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // El servicio del sorteo lo compone DomainServicesConfiguration.
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/prize-draws")
            .WithApiVersionSet(versionSet)
            .WithTags("Raffles")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Sin policy: la fallback policy exige, eso sí, un usuario autenticado (spec 00 §10).
        group.MapGet("by-code/{code}", FindPrizeDrawByCode)
            .MapToApiVersion(1)
            .WithName("FindPrizeDrawByCode")
            .WithSummary(RaffleEndpointsDescriptions.FindPrizeDrawByCodeSummary)
            .WithDescription(RaffleEndpointsDescriptions.FindPrizeDrawByCodeDescription)
            .Produces<PrizeDrawLookupResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> FindPrizeDrawByCode(
        [FromRoute] string code,
        [FromServices] IRaffleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.FindDrawByCodeAsync(code, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.Ok(PrizeDrawLookupResponse.From(result.Value));
    }
}
