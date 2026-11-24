using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// La lista de acceso de la puerta (spec <c>09</c> §5.1, PGTI-128). Su propio rol,
/// <c>check-in:read</c>, porque <c>convocatorias:read</c> abriría la lista de participantes con sus
/// direcciones a quien solo tiene que encontrar a alguien en la puerta.
/// </summary>
public sealed class DoorListEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Solo lectura, y la paginación se topa en el servicio: no hay nada que validar.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapGet($"{ApiRoutes.InternalPrefix}/events/{{eventId:guid}}/registrations/door", List)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Check-in")
            .WithName("ListDoorRegistrations")
            .WithSummary(DoorListEndpointsDescriptions.ListSummary)
            .WithDescription(DoorListEndpointsDescriptions.ListDescription)
            .RequireAuthorization(AuthorizationPolicies.CheckInRead)
            .Produces<DoorListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> List(
        [FromRoute] Guid eventId,
        [AsParameters] ListDoorRegistrationsRequest request,
        [FromServices] IDoorListService doorList,
        CancellationToken cancellationToken)
    {
        var result = await doorList.ListAsync(
            eventId,
            request.Search,
            request.CheckedIn,
            request.Page ?? 1,
            request.PageSize ?? IDoorListService.DefaultPageSize,
            cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.Ok(ToResponse(result.Value));
    }

    /// <summary>Campo a campo, para que la dirección con la que se busca no tenga por dónde salir.</summary>
    private static DoorListResponse ToResponse(DoorList list) => new()
    {
        Totals   = list.Totals,
        Items    = [.. list.Page.Items.Select(row => new DoorListItemResponse
        {
            RegistrationId = row.RegistrationId,
            FirstName      = row.FirstName,
            LastName       = row.LastName,
            Company        = row.Company,
            Reference      = row.Reference,
            CheckedInAt    = row.CheckedInAt,
        })],
        Total    = list.Page.Total,
        Page     = list.Page.Page,
        PageSize = list.Page.PageSize,
    };
}
