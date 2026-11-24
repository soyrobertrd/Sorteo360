using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Identity;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.RegistrationWindowEndpoints;

/// <summary>
/// Cerrar y reabrir la puerta de inscripción de un evento (spec <c>06</c> §5.2).
/// <para>
/// <b>Dos verbos y no un <c>PUT</c> con <c>{ open: false }</c></b>: un <c>PUT</c> se lee como
/// idempotente e invita a una pantalla a mandar el estado actual en cada guardado, lo que sellaría
/// <c>reopened_by_id</c> con quien abrió la página por última vez. Con dos verbos cada sello tiene
/// un solo escritor.
/// </para>
/// <para>
/// <b><c>campaigns:write</c> y no <c>campaigns:launch</c></b>: lo que protege lanzar es escribirle
/// a cientos de personas, que no tiene vuelta. Esto se deshace con un clic, y quien lo necesita es
/// la coordinadora de la OPT mirando el conteo de inscritos.
/// </para>
/// </summary>
public sealed class RegistrationWindowEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Ninguna de las dos rutas lleva cuerpo: no hay nada que validar.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup($"{ApiRoutes.InternalPrefix}/events/{{eventId:guid}}")
            .WithApiVersionSet(versionSet)
            .WithTags("Registration window")
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("close-registrations", CloseRegistrations)
            .MapToApiVersion(1)
            .WithName("CloseEventRegistrations")
            .WithSummary(RegistrationWindowEndpointsDescriptions.CloseRegistrationsSummary)
            .WithDescription(RegistrationWindowEndpointsDescriptions.CloseRegistrationsDescription)
            .Produces<RegistrationWindowResponse>(StatusCodes.Status200OK);

        group.MapPost("reopen-registrations", ReopenRegistrations)
            .MapToApiVersion(1)
            .WithName("ReopenEventRegistrations")
            .WithSummary(RegistrationWindowEndpointsDescriptions.ReopenRegistrationsSummary)
            .WithDescription(RegistrationWindowEndpointsDescriptions.ReopenRegistrationsDescription)
            .Produces<RegistrationWindowResponse>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> CloseRegistrations(
        [FromRoute] Guid eventId,
        [FromServices] IRegistrationWindowRepository windows,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await windows.CloseRegistrationsAsync(eventId, currentUser.Id, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> ReopenRegistrations(
        [FromRoute] Guid eventId,
        [FromServices] IRegistrationWindowRepository windows,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await windows.ReopenRegistrationsAsync(eventId, currentUser.Id, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.Ok(ToResponse(result.Value));
    }

    private static RegistrationWindowResponse ToResponse(RegistrationWindow window) => new()
    {
        EventId      = window.EventId,
        Open         = window.IsOpen,
        ClosedAt     = window.RegistrationsClosedAt,
        ClosedById   = window.RegistrationsClosedById,
        ReopenedAt   = window.ReopenedAt,
        ReopenedById = window.ReopenedById
    };
}
