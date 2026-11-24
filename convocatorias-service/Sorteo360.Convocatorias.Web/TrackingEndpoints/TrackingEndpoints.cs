using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Tracking;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.TrackingEndpoints;

/// <summary>
/// El resumen de un evento (spec <c>07</c> §2): a quién invitamos, quién se inscribió, quién no, qué
/// pide atención y si la puerta está abierta, en una sola llamada.
/// </summary>
public sealed class TrackingEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Solo lectura y sin cuerpo: no hay nada que validar.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapGet($"{ApiRoutes.InternalPrefix}/events/{{eventId:guid}}/tracking", Get)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Tracking")
            .WithName("GetEventTracking")
            .WithSummary(TrackingEndpointsDescriptions.GetSummary)
            .WithDescription(TrackingEndpointsDescriptions.GetDescription)
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .Produces<TrackingResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> Get(
        [FromRoute] Guid eventId,
        [FromServices] ITrackingService tracking,
        CancellationToken cancellationToken)
    {
        var result = await tracking.GetAsync(eventId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.Ok(ToResponse(result.Value));
    }

    private static TrackingResponse ToResponse(EventTracking tracking) => new()
    {
        Campaigns = [.. tracking.Campaigns.Select(summary => new TrackingCampaignResponse
        {
            Id             = summary.Campaign.Id,
            TemplateKey    = summary.Campaign.TemplateKey,
            State          = summary.Campaign.State,
            SentAt         = summary.Campaign.SentAt,
            RecipientCount = summary.RecipientCount,
            ExclusionCount = summary.ExclusionCount,
        })],
        People             = tracking.Rollup.People,
        Registrations      = tracking.Rollup.Registrations,
        NotRegistered      = tracking.Rollup.NotRegistered,
        NeedsAttention     = tracking.NeedsAttention,
        RegistrationWindow = new TrackingWindowResponse(
            tracking.RegistrationWindow.IsOpen,
            tracking.RegistrationWindow.RegistrationsClosedAt,
            tracking.RegistrationWindow.RegistrationsClosedById),
    };
}
