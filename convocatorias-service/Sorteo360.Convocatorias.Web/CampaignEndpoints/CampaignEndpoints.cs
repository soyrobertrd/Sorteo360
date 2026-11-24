using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Identity;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Convocatorias.Web.Validators;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

// CSharpFunctionalExtensions exporta su propio IResult, que choca con el de Minimal APIs.
// Se importa por alias el único tipo que hace falta en vez de traer el namespace entero.
using CampaignResult = CSharpFunctionalExtensions.Result<
    CSharpFunctionalExtensions.Maybe<Sorteo360.Convocatorias.Core.Campaigns.CampaignSummary>,
    Sorteo360.Platform.Errors.BaseError>;

namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// Las campañas: armarlas, construirles la lista y mandarlas.
/// <para>
/// <b>No hay someter, retirar, aprobar ni rechazar.</b> El cliente retiró el paso de aprobación
/// el 2026-09-07 (ADR-0017) y los cuatro actos se plegaron en uno, lanzar, con su propio rol.
/// Nada los reemplazó uno a uno.
/// </para>
/// <para>
/// <b>Dos roles, no uno</b>: <c>campaigns:write</c> arma y <c>campaigns:launch</c> manda. Los
/// dos van hoy en los tres compuestos, así que nadie queda acotado el día uno; la división
/// existe para que acotar mañana sea un cambio de consola sin código detrás.
/// </para>
/// </summary>
public sealed class CampaignEndpoints : IEndpoint
{
    /// <summary>La ruta del grupo, nombrada una vez: la mapea y además arma el <c>Location</c>.</summary>
    private const string CampaignGroupRoute = $"{ApiRoutes.InternalPrefix}/campaigns";

    /// <summary>El segmento de versión de la plantilla de ruta, que el <c>Location</c> resuelve.</summary>
    private const string VersionToken = "v{version:apiVersion}";

    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<CreateCampaignRequest>, CreateCampaignRequestValidator>();
        services.AddScoped<IValidator<UpdateCampaignRequest>, UpdateCampaignRequestValidator>();
        services.AddScoped<IValidator<SelectRecipientsRequest>, SelectRecipientsRequestValidator>();
        services.AddScoped<IValidator<LaunchCampaignRequest>, LaunchCampaignRequestValidator>();
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup(CampaignGroupRoute)
            .WithApiVersionSet(versionSet)
            .WithTags("Campaigns")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("", List)
            .MapToApiVersion(1)
            .WithName("ListCampaigns")
            .WithSummary(CampaignEndpointsDescriptions.ListSummary)
            .WithDescription(CampaignEndpointsDescriptions.ListDescription)
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .Produces<PagedResponse<CampaignResponse>>(StatusCodes.Status200OK);

        group.MapPost("", Create)
            .MapToApiVersion(1)
            .WithName("CreateCampaign")
            .WithSummary(CampaignEndpointsDescriptions.CreateSummary)
            .WithDescription(CampaignEndpointsDescriptions.CreateDescription)
            .WithValidation<CreateCampaignRequest>()
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .Produces<CampaignResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("{id:guid}", Get)
            .MapToApiVersion(1)
            .WithName("GetCampaign")
            .WithSummary(CampaignEndpointsDescriptions.GetSummary)
            .WithDescription(CampaignEndpointsDescriptions.GetDescription)
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .Produces<CampaignResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("{id:guid}", Update)
            .MapToApiVersion(1)
            .WithName("UpdateCampaign")
            .WithSummary(CampaignEndpointsDescriptions.UpdateSummary)
            .WithDescription(CampaignEndpointsDescriptions.UpdateDescription)
            .WithValidation<UpdateCampaignRequest>()
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .Produces<CampaignResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("{id:guid}/recipients/select", SelectRecipients)
            .MapToApiVersion(1)
            .WithName("SelectCampaignRecipients")
            .WithSummary(CampaignEndpointsDescriptions.SelectRecipientsSummary)
            .WithDescription(CampaignEndpointsDescriptions.SelectRecipientsDescription)
            .WithValidation<SelectRecipientsRequest>()
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .Produces<RecipientSelectionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapDelete("{id:guid}/recipients/{recipientId:guid}", RemoveRecipient)
            .MapToApiVersion(1)
            .WithName("RemoveCampaignRecipient")
            .WithSummary(CampaignEndpointsDescriptions.RemoveRecipientSummary)
            .WithDescription(CampaignEndpointsDescriptions.RemoveRecipientDescription)
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("{id:guid}/launch", Launch)
            .MapToApiVersion(1)
            .WithName("LaunchCampaign")
            .WithSummary(CampaignEndpointsDescriptions.LaunchSummary)
            .WithDescription(CampaignEndpointsDescriptions.LaunchDescription)
            .WithValidation<LaunchCampaignRequest>()
            .RequireAuthorization(AuthorizationPolicies.CampaignsLaunch)
            .Produces<CampaignResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Las dos salidas de un envío a medias (spec 04 §5). Piden `campaigns:write` y no
        // `campaigns:launch`: mandan la cola de una lista que alguien ya lanzó, así que desatascan
        // en vez de decidir, y son la única salida porque nada de este servicio reintenta solo.
        group.MapPost("{id:guid}/resume-handoff", ResumeHandoff)
            .MapToApiVersion(1)
            .WithName("ResumeCampaignHandoff")
            .WithSummary(CampaignEndpointsDescriptions.ResumeHandoffSummary)
            .WithDescription(CampaignEndpointsDescriptions.ResumeHandoffDescription)
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .Produces<CampaignResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("{id:guid}/retry-failed", RetryFailed)
            .MapToApiVersion(1)
            .WithName("RetryCampaignFailedRecipients")
            .WithSummary(CampaignEndpointsDescriptions.RetryFailedSummary)
            .WithDescription(CampaignEndpointsDescriptions.RetryFailedDescription)
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .Produces<CampaignResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("{id:guid}/cancel", Cancel)
            .MapToApiVersion(1)
            .WithName("CancelCampaign")
            .WithSummary(CampaignEndpointsDescriptions.CancelSummary)
            .WithDescription(CampaignEndpointsDescriptions.CancelDescription)
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .Produces<CampaignResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> List(
        [AsParameters] ListCampaignsRequest request,
        [FromServices] ICampaignRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.ListAsync(
            new CampaignQuery(request.Page ?? 1, request.PageSize ?? 20, request.EventId, request.State),
            cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(new PagedResponse<CampaignResponse>
        {
            Items = [.. result.Value.Items.Select(ToResponse)],
            Total = result.Value.Total,
            Page = result.Value.Page,
            PageSize = result.Value.PageSize,
        });
    }

    private static async Task<IResult> Create(
        HttpContext httpContext,
        [FromBody] CreateCampaignRequest request,
        [FromServices] ICampaignRepository repository,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var created = await repository.CreateAsync(
            new CreateCampaignDto(
                request.EventId,
                request.TemplateKey!,
                request.Language!,
                request.Subject,
                request.Message,
                currentUser.Id),
            cancellationToken);

        if (created.IsFailure)
            return created.Error.ToProblem();

        var campaign = await repository.GetAsync(created.Value, cancellationToken);

        if (campaign.IsFailure)
            return campaign.Error.ToProblem();

        // Se acaba de crear, así que el Maybe trae valor; se comprueba igual porque
        // `.Value` sobre un Maybe vacío revienta, y un 404 miente menos que un 500.
        if (!campaign.Value.HasValue)
            return Results.NotFound();

        return Results.Created(LocationOf(created.Value, httpContext), ToResponse(campaign.Value.Value));
    }

    private static async Task<IResult> Get(
        [FromRoute] Guid id,
        [FromServices] ICampaignRepository repository,
        CancellationToken cancellationToken)
    {
        return OkOrNotFound(await repository.GetAsync(id, cancellationToken));
    }

    private static async Task<IResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateCampaignRequest request,
        [FromServices] ICampaignRepository repository,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        return OkOrNotFound(await repository.UpdateAsync(
            new UpdateCampaignDto(id, request.TemplateKey, request.Language, request.Subject, request.Message, currentUser.Id),
            cancellationToken));
    }

    private static async Task<IResult> SelectRecipients(
        [FromRoute] Guid id,
        [FromBody] SelectRecipientsRequest request,
        [FromServices] ICampaignRepository repository,
        [FromServices] ICrmContactDirectory directory,
        [FromServices] IContactListReader contactLists,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // Una lista de contactos se reduce a sus ids, y desde ahí es la selección por ids: la
        // misma lectura y la misma exclusión (PGTI-173).
        var contactIds = request.ContactIds;

        if (request.ContactListId is { } contactListId)
        {
            var members = await contactLists.ReadOwnMemberIdsAsync(
                contactListId,
                currentUser.OptCode,
                cancellationToken);

            if (members.IsFailure)
                return members.Error.ToProblem();

            contactIds = members.Value;
        }

        // El CRM se lee antes de tocar la base: si no contesta, la petición falla entera y no
        // deja lista a medias (spec 03 §6.2). La guarda de borrador corre después, dentro de la
        // transacción que escribe, que es donde tiene que correr para no dejar pasar dos
        // selecciones simultáneas contra un borrador que dejó de serlo.
        var contacts = contactIds is not null
            ? await directory.ListContactsByIdAsync(contactIds, cancellationToken)
            : await directory.ListContactsAsync(
                request.Classifications ?? [],
                request.CompanyCategories ?? [],
                cancellationToken);

        if (contacts.IsFailure)
            return contacts.Error.ToProblem();

        var selection = await repository.SelectRecipientsAsync(id, contacts.Value, cancellationToken);

        if (selection.IsFailure)
            return selection.Error.ToProblem();

        if (!selection.Value.HasValue)
            return Results.NotFound();

        var campaign = await repository.GetAsync(id, cancellationToken);

        if (campaign.IsFailure)
            return campaign.Error.ToProblem();

        return campaign.Value.HasValue
            ? Results.Ok(ToResponse(selection.Value.Value, campaign.Value.Value))
            : Results.NotFound();
    }

    private static async Task<IResult> RemoveRecipient(
        [FromRoute] Guid id,
        [FromRoute] Guid recipientId,
        [FromServices] ICampaignRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.RemoveRecipientAsync(id, recipientId, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value is RemoveRecipientOutcome.Removed
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static async Task<IResult> Launch(
        [FromRoute] Guid id,
        [FromBody] LaunchCampaignRequest request,
        [FromServices] ICampaignLaunchService launch,
        [FromServices] ICurrentUser currentUser,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // El orden entero —validar, vaciar bajas, recomprobar contactabilidad, leer el evento y
        // recién entonces saltar a `sending`— es del servicio y no de acá: son pasos que se
        // intercalan con dos vecinos y no caben en una transacción (spec 04 §1).
        var result = await launch.LaunchAsync(
            new LaunchCampaignDto(id, request.ExpectedRecipientCount!.Value, currentUser.Id, currentUser.OptCode),
            cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        if (!result.Value.HasValue)
            return Results.NotFound();

        // 202 y no 200: la campaña quedó en `sending` y la entrega ocurre después de contestar.
        // El Location apunta a la campaña, que es donde quien lanza mira cómo va: el cuerpo de
        // esta respuesta es de este instante y deja de ser cierto en cuanto el worker avanza.
        return Results.Accepted(LocationOf(id, httpContext), ToResponse(result.Value.Value));
    }

    private static async Task<IResult> ResumeHandoff(
        [FromRoute] Guid id,
        [FromServices] ICampaignRepository repository,
        [FromServices] ICampaignHandoffQueue queue,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        return EnqueueOrRefuse(
            await repository.ResumeHandoffAsync(id, cancellationToken),
            id,
            queue,
            httpContext);
    }

    private static async Task<IResult> RetryFailed(
        [FromRoute] Guid id,
        [FromServices] ICampaignRepository repository,
        [FromServices] ICampaignHandoffQueue queue,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        return EnqueueOrRefuse(
            await repository.RetryFailedRecipientsAsync(id, cancellationToken),
            id,
            queue,
            httpContext);
    }

    /// <summary>
    /// El desenlace que comparten las dos rutas de desatasco: el fallo se traduce, un id que no
    /// existe es 404, y lo demás encola y contesta 202.
    /// <para>
    /// <b>Se encola después de que el repositorio haya dicho que sí</b>, y no antes: encolar
    /// primero pondría en el canal una campaña que la guarda de estado iba a rechazar, y el worker
    /// la leería igual. Y el <c>Enqueue</c> no admite token a propósito, porque el de acá es el de
    /// la petición y se cancela al cerrarse la pestaña (ADR-0018).
    /// </para>
    /// </summary>
    private static IResult EnqueueOrRefuse(
        CampaignResult result,
        Guid id,
        ICampaignHandoffQueue queue,
        HttpContext httpContext)
    {
        if (result.IsFailure)
            return result.Error.ToProblem();

        if (!result.Value.HasValue)
            return Results.NotFound();

        queue.Enqueue(id);

        // 202 y no 200, por lo mismo que el lanzamiento: el cuerpo es de este instante y deja de
        // ser cierto en cuanto el worker avanza, así que el Location apunta a donde se mira cómo va.
        return Results.Accepted(LocationOf(id, httpContext), ToResponse(result.Value.Value));
    }

    /// <summary>
    /// La URL de una campaña, con el segmento de versión resuelto al de la petición. La arman el
    /// <c>Location</c> del 201 de crear y el del 202 de lanzar, de la misma ruta con que se mapeó
    /// el grupo: escribirla a mano en dos sitios es cómo una de las dos se queda en v1.
    /// </summary>
    private static string LocationOf(Guid id, HttpContext httpContext)
    {
        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";

        return $"/{CampaignGroupRoute.Replace(VersionToken, $"v{version}", StringComparison.Ordinal)}/{id}";
    }

    private static async Task<IResult> Cancel(
        [FromRoute] Guid id,
        [FromServices] ICampaignRepository repository,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        return OkOrNotFound(await repository.CancelAsync(id, currentUser.Id, cancellationToken));
    }

    /// <summary>
    /// El desenlace que comparten leer, editar y cancelar: el fallo se traduce, un id que no
    /// existe es 404, y lo demás es la campaña.
    /// </summary>
    private static IResult OkOrNotFound(CampaignResult result)
    {
        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(ToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static CampaignResponse ToResponse(CampaignSummary summary) => new()
    {
        Id = summary.Campaign.Id,
        EventId = summary.Campaign.EventId,
        Subject = summary.Campaign.Subject,
        Message = summary.Campaign.Message,
        TemplateKey = summary.Campaign.TemplateKey,
        Language = summary.Campaign.Language,
        State = summary.Campaign.State,
        RecipientCount = summary.RecipientCount,
        ExclusionCount = summary.ExclusionCount,
        LaunchedById = summary.Campaign.LaunchedById,
        LaunchedAt = summary.Campaign.LaunchedAt,
        SentAt = summary.Campaign.SentAt,
        CreatedAt = summary.Campaign.CreatedAt,
        UpdatedAt = summary.Campaign.UpdatedAt,
    };

    private static RecipientSelectionResponse ToResponse(RecipientSelection selection, CampaignSummary summary) => new()
    {
        Matched = selection.Matched,
        Added = selection.Added,
        Excluded = selection.Excluded,
        Skipped = selection.Skipped,
        ExcludedByReason = selection.ExcludedByReason,
        Campaign = ToResponse(summary),
    };
}
