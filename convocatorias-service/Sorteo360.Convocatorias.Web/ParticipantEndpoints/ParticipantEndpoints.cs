using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Participants;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Sorteo360.Convocatorias.Web.ReconciliationEndpoints;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.ParticipantEndpoints;

/// <summary>
/// La lista de participantes de un evento (spec <c>07</c> §3): a quién invitamos, quién se
/// inscribió y quién no, como tres filtros sobre una sola ruta.
/// <para>
/// <b>No hay lista de destinatarios por campaña aparte</b> (§1): es esta misma filtrada por
/// <c>campaignId</c>, así que hay una forma de fila, un orden y un juego de filtros, no dos que se
/// separen. La ruta por campaña es esa misma lista buscada por la campaña y no por el evento,
/// porque un mensaje directo no tiene evento del que colgarla (PGTI-178).
/// </para>
/// </summary>
public sealed class ParticipantEndpoints : IEndpoint
{
    private const string CsvContentType = "text/csv; charset=utf-8";

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

        app.MapGet($"{ApiRoutes.InternalPrefix}/events/{{eventId:guid}}/participants", List)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Tracking")
            .WithName("ListEventParticipants")
            .WithSummary(ParticipantEndpointsDescriptions.ListSummary)
            .WithDescription(ParticipantEndpointsDescriptions.ListDescription)
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .Produces<PagedResponse<ParticipantResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet($"{ApiRoutes.InternalPrefix}/events/{{eventId:guid}}/participants.csv", ExportCsv)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Tracking")
            .WithName("ExportEventParticipantsCsv")
            .WithSummary(ParticipantEndpointsDescriptions.ExportCsvSummary)
            .WithDescription(ParticipantEndpointsDescriptions.ExportCsvDescription)
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .Produces(StatusCodes.Status200OK, contentType: CsvContentType)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet($"{ApiRoutes.InternalPrefix}/campaigns/{{campaignId:guid}}/recipients", ListCampaignRecipients)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Campaigns")
            .WithName("ListCampaignRecipients")
            .WithSummary(ParticipantEndpointsDescriptions.ListCampaignRecipientsSummary)
            .WithDescription(ParticipantEndpointsDescriptions.ListCampaignRecipientsDescription)
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .Produces<PagedResponse<ParticipantResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    /// <summary>Arma el filtro con la consulta y devuelve la página pedida de la lista unida.</summary>
    private static async Task<IResult> List(
        [FromRoute] Guid eventId,
        [AsParameters] ListParticipantsRequest request,
        [FromServices] IParticipantService participants,
        CancellationToken cancellationToken)
    {
        var result = await participants.ListPageAsync(
            eventId, request.ToFilter(), request.Page ?? 1, request.PageSize ?? 20, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(ToPagedResponse(result.Value));
    }

    /// <summary>La página de destinatarios de una campaña, con o sin evento; <c>404</c> si no existe.</summary>
    private static async Task<IResult> ListCampaignRecipients(
        [FromRoute] Guid campaignId,
        [AsParameters] ReconciliationPageRequest request,
        [FromServices] IParticipantService participants,
        CancellationToken cancellationToken)
    {
        var result = await participants.ListCampaignRecipientsPageAsync(
            campaignId, request.PageNumber, request.Size, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.TryGetValue(out var page) ? Results.Ok(ToPagedResponse(page)) : Results.NotFound();
    }

    private static PagedResponse<ParticipantResponse> ToPagedResponse(PagedResult<Participant> page) => new()
    {
        Items = [.. page.Items.Select(ToResponse)],
        Total = page.Total,
        Page = page.Page,
        PageSize = page.PageSize,
    };

    /// <summary>
    /// Todas las filas de la lista bajo los mismos filtros, sin paginar, escritas directo en la
    /// respuesta. La unión se lee entera antes de escribir el primer byte, así que un fallo todavía
    /// responde con su código y no con un CSV cortado.
    /// </summary>
    private static async Task<IResult> ExportCsv(
        [FromRoute] Guid eventId,
        [AsParameters] ListParticipantsRequest request,
        [FromServices] IParticipantService participants,
        [FromServices] TimeProvider clock,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var rows = await participants.ListAsync(eventId, request.ToFilter(), cancellationToken);

        if (rows.IsFailure)
            return rows.Error.ToProblem();

        // A mano y no con fileDownloadName, que omite las comillas que pide la spec.
        var fileName = ParticipantCsv.FileName(eventId, clock.GetUtcNow());
        httpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

        return Results.Stream(
            output => ParticipantCsv.WriteAsync(rows.Value, output, cancellationToken),
            CsvContentType);
    }

    /// <summary>La fila de dominio en la forma del contrato; el que reenvió pasa a ser un objeto.</summary>
    private static ParticipantResponse ToResponse(Participant participant) => new()
    {
        Email          = participant.Email,
        FirstName      = participant.FirstName,
        LastName       = participant.LastName,
        Company        = participant.Company,
        Origin         = participant.Origin,
        SendStatus     = participant.SendStatus,
        Registered     = participant.Registered,
        RegisteredAt   = participant.RegisteredAt,
        Reference      = participant.Reference,
        ReferredBy     = participant.ReferredByName is { } name ? new ParticipantReferrerResponse(name) : null,
        CampaignIds    = participant.CampaignIds,
        CrmSyncPending = participant.CrmSyncPending,
        DetailsDiffer  = participant.DetailsDiffer,
        CheckedInAt    = participant.CheckedInAt,
        DeliveryStatus = participant.DeliveryStatus,
    };
}
