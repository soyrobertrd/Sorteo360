using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// La lista de check-ins que lee la rifa de events-service (spec <c>09</c> §7, PGTI-130). Clase propia
/// y no una ruta más del grupo de <see cref="CheckInWriteEndpoints"/>, que pide
/// <c>check-in:write</c>: en ese grupo la lectura exigiría los dos roles.
/// </summary>
public sealed class CheckInServiceReadEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Solo lectura, sin parámetros que validar.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapGet($"{ApiRoutes.InternalPrefix}/events/{{eventId:guid}}/check-ins", List)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Check-in")
            .WithName("ListEventCheckIns")
            .WithSummary(CheckInServiceReadEndpointsDescriptions.ListSummary)
            .WithDescription(CheckInServiceReadEndpointsDescriptions.ListDescription)
            .RequireAuthorization(AuthorizationPolicies.CheckInServiceRead)
            .Produces<CheckInListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> List(
        [FromRoute] Guid eventId,
        [FromServices] ICheckInListRepository checkIns,
        CancellationToken cancellationToken)
    {
        var result = await checkIns.ReadAsync(eventId, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(new CheckInListResponse { Items = [.. result.Value.Select(ToItem)] });
    }

    private static CheckInListItem ToItem(CheckInListRow row) => new()
    {
        RegistrationId  = row.RegistrationId,
        EmailNormalized = row.EmailNormalized,
        FirstName       = row.FirstName,
        LastName        = row.LastName,
        Company         = row.Company,
        ContactId       = row.ContactId,
        Origin          = row.Origin,
        CheckedInAt     = row.CheckedInAt,
    };
}
