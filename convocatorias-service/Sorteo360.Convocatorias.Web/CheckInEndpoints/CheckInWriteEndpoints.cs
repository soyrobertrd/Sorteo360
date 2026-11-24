using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Identity;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Convocatorias.Web.Validators;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

// CSharpFunctionalExtensions exporta su propio IResult, que choca con el de Minimal APIs.
using CheckInResult = CSharpFunctionalExtensions.Result<
    Sorteo360.Convocatorias.Core.CheckIns.CheckIn,
    Sorteo360.Platform.Errors.BaseError>;

namespace Sorteo360.Convocatorias.Web.CheckInEndpoints;

/// <summary>
/// La escritura de la puerta (spec <c>09</c> §3, §4 y §5): el escaneo, el check-in elegido en la
/// lista de acceso y la deshecha. Las decisiones son de <see cref="ICheckInService"/>; acá solo se
/// traduce su resultado a HTTP.
/// </summary>
public sealed class CheckInWriteEndpoints : IEndpoint
{
    private const string GroupRoute = $"{ApiRoutes.InternalPrefix}/events/{{eventId:guid}}/check-ins";

    private const string VersionToken = "v{version:apiVersion}";

    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<CheckInRequest>, CheckInRequestValidator>();
        services.AddScoped<IValidator<CheckInByRegistrationRequest>, CheckInByRegistrationRequestValidator>();
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup(GroupRoute)
            .WithApiVersionSet(versionSet)
            .WithTags("Check-in")
            .RequireAuthorization(AuthorizationPolicies.CheckInWrite)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("", Create)
            .MapToApiVersion(1)
            .WithName("CreateCheckIn")
            .WithSummary(CheckInWriteEndpointsDescriptions.CreateSummary)
            .WithDescription(CheckInWriteEndpointsDescriptions.CreateDescription)
            .WithValidation<CheckInRequest>()
            .Produces<CheckInResponse>(StatusCodes.Status201Created)
            .Produces<CheckInResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("by-registration", CreateByRegistration)
            .MapToApiVersion(1)
            .WithName("CreateCheckInByRegistration")
            .WithSummary(CheckInWriteEndpointsDescriptions.CreateByRegistrationSummary)
            .WithDescription(CheckInWriteEndpointsDescriptions.CreateByRegistrationDescription)
            .WithValidation<CheckInByRegistrationRequest>()
            .Produces<CheckInResponse>(StatusCodes.Status201Created)
            .Produces<CheckInResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("{registrationId:guid}", Undo)
            .MapToApiVersion(1)
            .WithName("UndoCheckIn")
            .WithSummary(CheckInWriteEndpointsDescriptions.UndoSummary)
            .WithDescription(CheckInWriteEndpointsDescriptions.UndoDescription)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Create(
        [FromRoute] Guid eventId,
        [FromBody] CheckInRequest request,
        [FromServices] ICheckInService checkIns,
        [FromServices] ICurrentUser currentUser,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await checkIns.CheckInAsync(eventId, request.QrCode!, currentUser.Id, cancellationToken);

        return ToHttpResult(eventId, result, httpContext);
    }

    private static async Task<IResult> CreateByRegistration(
        [FromRoute] Guid eventId,
        [FromBody] CheckInByRegistrationRequest request,
        [FromServices] ICheckInService checkIns,
        [FromServices] ICurrentUser currentUser,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await checkIns.CheckInByRegistrationAsync(
            eventId, request.RegistrationId!.Value, currentUser.Id, cancellationToken);

        return ToHttpResult(eventId, result, httpContext);
    }

    /// <summary>
    /// La misma respuesta para las dos formas de hacer un check-in (spec <c>09</c> §5): <c>201</c> la
    /// primera vez y <c>200</c> las siguientes, con el mismo cuerpo.
    /// </summary>
    private static IResult ToHttpResult(Guid eventId, CheckInResult result, HttpContext httpContext)
    {
        if (result.IsFailure)
            return result.Error.ToProblem();

        var response = ToResponse(result.Value);

        // El 201 apunta a la URL que deshace este check-in, armada con los valores de la ruta y no
        // con la ruta pedida: desde by-registration, colgarle el id no apuntaría a nada. Una
        // repetición (200) no la lleva: no creó nada.
        return response.AlreadyCheckedIn
            ? Results.Ok(response)
            : Results.Created(UndoLocationOf(eventId, response.RegistrationId, httpContext), response);
    }

    private static string UndoLocationOf(Guid eventId, Guid registrationId, HttpContext httpContext)
    {
        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        var group = GroupRoute
            .Replace(VersionToken, $"v{version}", StringComparison.Ordinal)
            .Replace("{eventId:guid}", eventId.ToString(), StringComparison.Ordinal);

        return $"/{group}/{registrationId}";
    }

    private static async Task<IResult> Undo(
        [FromRoute] Guid eventId,
        [FromRoute] Guid registrationId,
        [FromServices] ICheckInService checkIns,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await checkIns.UndoAsync(eventId, registrationId, currentUser.Id, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private static CheckInResponse ToResponse(CheckIn checkIn) => new()
    {
        RegistrationId   = checkIn.RegistrationId,
        FirstName        = checkIn.FirstName,
        LastName         = checkIn.LastName,
        Company          = checkIn.Company,
        Reference        = checkIn.Reference,
        CheckedInAt      = checkIn.CheckedInAt,
        CheckedInById    = checkIn.CheckedInById,
        AlreadyCheckedIn = checkIn.AlreadyCheckedIn,
    };
}
