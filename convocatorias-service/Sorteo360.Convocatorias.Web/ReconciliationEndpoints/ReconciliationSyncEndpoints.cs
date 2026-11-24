using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Core.Unsubscribe;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.ReconciliationEndpoints;

/// <summary>
/// Los dos botones que terminan a mano una escritura varada en el CRM (spec <c>07</c> §6.1 y
/// §6.2). <b>Nada en este servicio reintenta solo</b>, así que son la única salida de una
/// inscripción que nunca llegó al CRM o de una baja de la que el CRM nunca se enteró.
/// <para>
/// <b>Cada uno corre el camino de la ruta pública, no una copia</b>: el reintento vive en el mismo
/// servicio que la ruta pública y llama al mismo método privado, de modo que no hay dos caminos
/// que se separen con el tiempo.
/// </para>
/// </summary>
public sealed class ReconciliationSyncEndpoints : IEndpoint
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

        var group = app.MapGroup(ApiRoutes.InternalPrefix)
            .WithApiVersionSet(versionSet)
            .WithTags("Reconciliation")
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("registrations/{id:guid}/crm-sync", SyncRegistrationWithCrm)
            .MapToApiVersion(1)
            .WithName("SyncRegistrationWithCrm")
            .WithSummary(ReconciliationSyncEndpointsDescriptions.CrmSyncSummary)
            .WithDescription(ReconciliationSyncEndpointsDescriptions.CrmSyncDescription)
            .Produces<ReconciledRegistrationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("recipients/{id:guid}/unsubscribe-sync", SyncRecipientUnsubscribe)
            .MapToApiVersion(1)
            .WithName("SyncRecipientUnsubscribe")
            .WithSummary(ReconciliationSyncEndpointsDescriptions.UnsubscribeSyncSummary)
            .WithDescription(ReconciliationSyncEndpointsDescriptions.UnsubscribeSyncDescription)
            .Produces<ReconciledRecipientResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> SyncRegistrationWithCrm(
        [FromRoute] Guid id,
        [FromServices] IRegistrationService registrations,
        CancellationToken cancellationToken)
    {
        var result = await registrations.RetryCrmSyncAsync(id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(ToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static async Task<IResult> SyncRecipientUnsubscribe(
        [FromRoute] Guid id,
        [FromServices] IUnsubscribeService unsubscribes,
        CancellationToken cancellationToken)
    {
        var result = await unsubscribes.RetrySyncAsync(id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(ToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static ReconciledRecipientResponse ToResponse(RecipientUnsubscribe recipient) => new()
    {
        Id                  = recipient.RecipientId,
        CampaignId          = recipient.CampaignId,
        ContactId           = recipient.ContactId,
        UnsubscribedAt      = recipient.UnsubscribedAt,
        UnsubscribeSyncedAt = recipient.UnsubscribeSyncedAt
    };

    private static ReconciledRegistrationResponse ToResponse(CrmSyncRetry retry) => new()
    {
        Id                   = retry.Registration.Id,
        EventId              = retry.Registration.EventId,
        Reference            = retry.Registration.Reference,
        FirstName            = retry.Registration.FirstName,
        LastName             = retry.Registration.LastName,
        Email                = retry.Registration.Email,
        CrmContactId         = retry.Registration.CrmContactId,
        CrmSyncFailedAt      = retry.Registration.CrmSyncFailedAt,
        ContactDetailsDiffer = retry.Registration.ContactDetailsDiffer,
        CanBeEmailed         = retry.CanBeEmailed
    };
}
