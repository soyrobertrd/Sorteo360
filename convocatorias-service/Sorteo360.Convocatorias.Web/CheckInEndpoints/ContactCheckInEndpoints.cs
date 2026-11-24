using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Convocatorias.Web.Validators;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// Los eventos a los que asistió un contacto del CRM (PGTI-170). La asistencia vive acá porque acá
/// vive el check-in; el CRM la lee para la ficha del contacto y el conteo de su listado, y la de
/// una empresa sale de juntar la de sus contactos.
/// <para>
/// <b><c>convocatorias:read</c> y no un rol nuevo</b>: es la misma lectura de seguimiento que ya
/// tienen los tres compuestos que abren el CRM.
/// </para>
/// </summary>
public sealed class ContactCheckInEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<ListContactCheckInsRequest>, ListContactCheckInsRequestValidator>();
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapGet($"{ApiRoutes.InternalPrefix}/check-ins", List)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Check-in")
            .WithName("ListContactCheckIns")
            .WithSummary(ContactCheckInEndpointsDescriptions.ListSummary)
            .WithDescription(ContactCheckInEndpointsDescriptions.ListDescription)
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .WithValidation<ListContactCheckInsRequest>()
            .Produces<IReadOnlyList<ContactCheckInResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> List(
        [AsParameters] ListContactCheckInsRequest request,
        [FromServices] IContactCheckInRepository checkIns,
        CancellationToken cancellationToken)
    {
        var result = await checkIns.ListByContactsAsync(request.ContactIds!, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(result.Value.Select(ToResponse).ToList());
    }

    private static ContactCheckInResponse ToResponse(ContactCheckIn checkIn) => new()
    {
        ContactId   = checkIn.ContactId,
        EventId     = checkIn.EventId,
        CheckedInAt = checkIn.CheckedInAt,
    };
}
