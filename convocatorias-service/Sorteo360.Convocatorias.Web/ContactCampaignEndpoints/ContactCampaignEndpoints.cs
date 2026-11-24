using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Convocatorias.Web.Validators;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.ContactCampaignEndpoints;

/// <summary>
/// Las campañas que tuvieron a unos contactos del CRM en su lista (PGTI-176). La pestaña
/// Convocatorias de una empresa pregunta por todos sus contactos de una vez y se queda con lo que se
/// les envió mientras trabajaban ahí.
/// <para>
/// <b><c>convocatorias:read</c> y no un rol nuevo</b>: es la misma lectura de seguimiento que la de
/// asistencias (PGTI-170), que ya tienen los tres compuestos que abren el CRM.
/// </para>
/// </summary>
public sealed class ContactCampaignEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<ListContactCampaignsRequest>, ListContactCampaignsRequestValidator>();
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapGet($"{ApiRoutes.InternalPrefix}/campaign-recipients", List)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Tracking")
            .WithName("ListContactCampaigns")
            .WithSummary(ContactCampaignEndpointsDescriptions.ListSummary)
            .WithDescription(ContactCampaignEndpointsDescriptions.ListDescription)
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .WithValidation<ListContactCampaignsRequest>()
            .Produces<IReadOnlyList<ContactCampaignResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> List(
        [AsParameters] ListContactCampaignsRequest request,
        [FromServices] IContactCampaignRecipientRepository recipients,
        [FromServices] IDeliveryStatusReader delivery,
        CancellationToken cancellationToken)
    {
        var result = await recipients.ListByContactsAsync(request.ContactIds!, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        // La entrega es de notification-service y se lee en vivo; si no contesta, todo dice "sent".
        var known = await delivery.ReadAsync(
            [.. result.Value.Where(r => r.NotificationId is not null).Select(r => r.NotificationId!.Value).Distinct()],
            cancellationToken);

        return Results.Ok(result.Value.Select(recipient => ToResponse(recipient, known)).ToList());
    }

    private static ContactCampaignResponse ToResponse(
        ContactCampaignRecipient recipient,
        IReadOnlyDictionary<Guid, string> known) => new()
    {
        ContactId  = recipient.ContactId,
        CampaignId = recipient.CampaignId,
        EventId    = recipient.EventId,
        Subject    = recipient.Subject,
        LaunchedAt = recipient.LaunchedAt,
        SendStatus = recipient.SendStatus,
        Registered = recipient.Registered,
        DeliveryStatus = CampaignDeliveryStatus.Of(
            recipient.SendStatus,
            recipient.NotificationId is { } id ? [id] : [],
            known),
    };
}
