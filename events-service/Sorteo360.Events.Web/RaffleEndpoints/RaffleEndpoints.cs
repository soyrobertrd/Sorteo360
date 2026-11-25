using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.Filters;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// El sorteo de un evento, como subrecurso único del evento. Las escrituras de armado exigen
/// <c>raffles:write</c>; las lecturas no tienen política propia y las cubre la fallback policy.
/// </summary>
public sealed class RaffleEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<UpdateRaffleRequest>, Validators.UpdateRaffleRequestValidator>();
        services.AddScoped<IValidator<ReissueRaffleRequest>, Validators.ReissueRaffleRequestValidator>();
        services.AddScoped<IValidator<SetCertificateSignersRequest>, Validators.SetCertificateSignersRequestValidator>();
        services.AddScoped<IValidator<SetAttendeeOverrideRequest>, Validators.SetAttendeeOverrideRequestValidator>();
        services.AddScoped<IValidator<RemoveAttendeeOverrideRequest>, Validators.RemoveAttendeeOverrideRequestValidator>();
        services.AddScoped<IValidator<BulkExcludeAttendeesRequest>, Validators.BulkExcludeAttendeesRequestValidator>();
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/events/{eventId:guid}/raffle")
            .WithApiVersionSet(versionSet)
            .WithTags("Raffles")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("", CreateRaffle)
            .MapToApiVersion(1)
            .WithName("CreateRaffle")
            .WithSummary(RaffleEndpointsDescriptions.CreateRaffleSummary)
            .WithDescription(RaffleEndpointsDescriptions.CreateRaffleDescription)
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("", DeleteRaffle)
            .MapToApiVersion(1)
            .WithName("DeleteRaffle")
            .WithSummary(RaffleEndpointsDescriptions.DeleteRaffleSummary)
            .WithDescription(RaffleEndpointsDescriptions.DeleteRaffleDescription)
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Sin policy: la fallback policy exige, eso sí, un usuario autenticado. Las dos lecturas
        // que calculan el bombo le preguntan al CRM por la lista negra, así que llevan su estado.
        // Las cuatro escrituras que devuelven el sorteo también: nombran a los patrocinadores de sus
        // premios (spec 00 §8).
        group.MapGet("", GetRaffle)
            .MapToApiVersion(1)
            .WithName("GetRaffle")
            .WithSummary(RaffleEndpointsDescriptions.GetRaffleSummary)
            .WithDescription(RaffleEndpointsDescriptions.GetRaffleDescription)
            .AddEndpointFilter<CrmDirectoryStatusFilter>()
            .Produces<RaffleResponse>();

        // Sin policy, igual que la lectura del sorteo.
        group.MapGet("attendees", GetAttendees)
            .MapToApiVersion(1)
            .WithName("GetRaffleAttendees")
            .WithSummary(RaffleEndpointsDescriptions.GetAttendeesSummary)
            .WithDescription(RaffleEndpointsDescriptions.GetAttendeesDescription)
            .AddEndpointFilter<CrmDirectoryStatusFilter>()
            .Produces<RaffleAttendeesResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Sin cuerpo, así que no hay validación; el 200 se declara igual que en el resto.
        group.MapPost("start", StartRaffle)
            .MapToApiVersion(1)
            .WithName("StartRaffle")
            .WithSummary(RaffleEndpointsDescriptions.StartRaffleSummary)
            .WithDescription(RaffleEndpointsDescriptions.StartRaffleDescription)
            .AddEndpointFilter<CrmDirectoryStatusFilter>()
            .RequireAuthorization(AuthorizationPolicies.RafflesDraw)
            .Produces<RaffleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // La vuelta atrás de start: sin cuerpo, con la misma política y la misma respuesta.
        group.MapPost("reopen", ReopenRaffle)
            .MapToApiVersion(1)
            .WithName("ReopenRaffle")
            .WithSummary(RaffleEndpointsDescriptions.ReopenRaffleSummary)
            .WithDescription(RaffleEndpointsDescriptions.ReopenRaffleDescription)
            .AddEndpointFilter<CrmDirectoryStatusFilter>()
            .RequireAuthorization(AuthorizationPolicies.RafflesDraw)
            .Produces<RaffleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // El cierre: sin cuerpo, con la política de la noche del sorteo y la misma respuesta. El 503
        // es el almacenamiento de archivos, donde queda el acta.
        group.MapPost("complete", CompleteRaffle)
            .MapToApiVersion(1)
            .WithName("CompleteRaffle")
            .WithSummary(RaffleEndpointsDescriptions.CompleteRaffleSummary)
            .WithDescription(RaffleEndpointsDescriptions.CompleteRaffleDescription)
            .AddEndpointFilter<CrmDirectoryStatusFilter>()
            .RequireAuthorization(AuthorizationPolicies.RafflesDraw)
            .Produces<RaffleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // La corrección del acta: las seis autoridades en un cuerpo y la misma respuesta que el cierre.
        // WithValidation quita el 200 inferido, así que se declara.
        group.MapPost("reissue", ReissueRaffle)
            .MapToApiVersion(1)
            .WithName("ReissueRaffleRecord")
            .WithSummary(RaffleEndpointsDescriptions.ReissueRaffleSummary)
            .WithDescription(RaffleEndpointsDescriptions.ReissueRaffleDescription)
            .AddEndpointFilter<CrmDirectoryStatusFilter>()
            .WithValidation<ReissueRaffleRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesDraw)
            .Produces<RaffleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("attendees/overrides", SetAttendeeOverride)
            .MapToApiVersion(1)
            .WithName("SetRaffleAttendeeOverride")
            .WithSummary(RaffleEndpointsDescriptions.SetOverrideSummary)
            .WithDescription(RaffleEndpointsDescriptions.SetOverrideDescription)
            .WithValidation<SetAttendeeOverrideRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("attendees/overrides/bulk-exclude", BulkExcludeAttendees)
            .MapToApiVersion(1)
            .WithName("BulkExcludeRaffleAttendees")
            .WithSummary(RaffleEndpointsDescriptions.BulkExcludeSummary)
            .WithDescription(RaffleEndpointsDescriptions.BulkExcludeDescription)
            .WithValidation<BulkExcludeAttendeesRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces<BulkExcludeAttendeesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapDelete("attendees/overrides", RemoveAttendeeOverride)
            .MapToApiVersion(1)
            .WithName("RemoveRaffleAttendeeOverride")
            .WithSummary(RaffleEndpointsDescriptions.RemoveOverrideSummary)
            .WithDescription(RaffleEndpointsDescriptions.RemoveOverrideDescription)
            .WithValidation<RemoveAttendeeOverrideRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("attendees/{attendeeId:guid}/reinstate", ReinstateAttendee)
            .MapToApiVersion(1)
            .WithName("ReinstateRaffleAttendee")
            .WithSummary(RaffleEndpointsDescriptions.ReinstateAttendeeSummary)
            .WithDescription(RaffleEndpointsDescriptions.ReinstateAttendeeDescription)
            .AddEndpointFilter<CrmDirectoryStatusFilter>()
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces<RaffleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Fuera del PATCH a propósito: el certificado se imprime sobre todo con el sorteo ya completado,
        // y un completado no admite el PATCH. Los firmantes no atestiguan nada, así que no lo esperan.
        group.MapPut("certificate-signers", SetCertificateSigners)
            .MapToApiVersion(1)
            .WithName("SetRaffleCertificateSigners")
            .WithSummary(RaffleEndpointsDescriptions.SetCertificateSignersSummary)
            .WithDescription(RaffleEndpointsDescriptions.SetCertificateSignersDescription)
            .WithValidation<SetCertificateSignersRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("display-fields", SetDisplayFields)
            .MapToApiVersion(1)
            .WithName("SetRaffleDisplayFields")
            .WithSummary("Configura los campos visibles del participante en pantalla y proyector.")
            .WithDescription("Editable únicamente mientras el sorteo está en borrador. Una vez iniciado, queda fija.")
            .AddEndpointFilter<CrmDirectoryStatusFilter>()
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces<RaffleResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("", UpdateRaffle)
            .MapToApiVersion(1)
            .WithName("UpdateRaffle")
            .WithSummary(RaffleEndpointsDescriptions.UpdateRaffleSummary)
            .WithDescription(RaffleEndpointsDescriptions.UpdateRaffleDescription)
            .WithValidation<UpdateRaffleRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> CreateRaffle(
        HttpContext httpContext,
        [FromRoute] Guid eventId,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // R9: la verificación y el conteo del tope del plan corren en la misma transacción que
        // crea el sorteo dentro de RaffleRepository.AddAsync.
        var result = await service.CreateAsync(eventId, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        return Results.Created($"/v{version}/events/{eventId}/raffle", result.Value);
    }

    private static async Task<IResult> DeleteRaffle(
        [FromRoute] Guid eventId,
        [FromServices] IRaffleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(eventId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private static async Task<IResult> GetRaffle(
        HttpContext httpContext,
        [FromRoute] Guid eventId,
        [FromServices] IRaffleService service,
        [FromServices] ICompanyDirectory companyDirectory,
        [FromServices] IContactNameDirectory contactNames,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(eventId, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        // R15: ETag basado en la versión/actividad del sorteo, rondas, premios y sorteos para responder 304 cuando no cambió.
        var detail = result.Value.Raffle;
        var prizeCount = 0;
        var drawCount = detail.Draws.Count;
        var latestActivityTicks = detail.Raffle.UpdatedAt.UtcTicks;
        foreach (var r in detail.Rounds)
        {
            if (r.UpdatedAt.UtcTicks > latestActivityTicks) latestActivityTicks = r.UpdatedAt.UtcTicks;
            foreach (var p in r.Prizes)
            {
                prizeCount++;
                if (p.UpdatedAt.UtcTicks > latestActivityTicks) latestActivityTicks = p.UpdatedAt.UtcTicks;
            }
        }
        foreach (var d in detail.Draws)
        {
            if (d.DrawnAt.UtcTicks > latestActivityTicks) latestActivityTicks = d.DrawnAt.UtcTicks;
            if (d.VoidedAt is { } v && v.UtcTicks > latestActivityTicks) latestActivityTicks = v.UtcTicks;
            foreach (var h in d.Handovers)
            {
                if (h.RedeemedAt.UtcTicks > latestActivityTicks) latestActivityTicks = h.RedeemedAt.UtcTicks;
                if (h.ReversedAt is { } rev && rev.UtcTicks > latestActivityTicks) latestActivityTicks = rev.UtcTicks;
            }
        }

        var etag = $"W/\"{detail.Raffle.Id:N}-{detail.Raffle.State}-{detail.Rounds.Count}-{prizeCount}-{drawCount}-{detail.NotarialRecords.Count}-{latestActivityTicks}\"";
        httpContext.Response.Headers.ETag = etag;

        var ifNoneMatch = httpContext.Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrEmpty(ifNoneMatch) && string.Equals(ifNoneMatch.Trim(), etag, StringComparison.Ordinal))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        return Results.Ok(await RaffleResponse.FromAsync(result.Value, companyDirectory, contactNames, operators, cancellationToken));
    }

    private static async Task<IResult> GetAttendees(
        [FromRoute] Guid eventId,
        [FromServices] IRaffleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAttendeesAsync(eventId, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(RaffleAttendeesResponse.From(result.Value));
    }

    private static async Task<IResult> StartRaffle(
        [FromRoute] Guid eventId,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICompanyDirectory companyDirectory,
        [FromServices] IContactNameDirectory contactNames,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var result = await service.StartAsync(eventId, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(await RaffleResponse.FromAsync(result.Value, companyDirectory, contactNames, operators, cancellationToken));
    }

    private static async Task<IResult> CompleteRaffle(
        [FromRoute] Guid eventId,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICompanyDirectory companyDirectory,
        [FromServices] IContactNameDirectory contactNames,
        [FromServices] IRaffleOperatorDirectory operators,
        [FromServices] ICurrentAccount currentAccount,
        [FromServices] RaffleWinnerNotificationWorker winnerNotifications,
        CancellationToken cancellationToken)
    {
        var result = await service.CompleteAsync(eventId, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        winnerNotifications.Enqueue(eventId, currentAccount.AccountId);
        return Results.Ok(await RaffleResponse.FromAsync(result.Value, companyDirectory, contactNames, operators, cancellationToken));
    }

    private static async Task<IResult> ReissueRaffle(
        [FromRoute] Guid eventId,
        [FromBody] ReissueRaffleRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICompanyDirectory companyDirectory,
        [FromServices] IContactNameDirectory contactNames,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var result = await service.ReissueAsync(eventId, request.ToAuthorities(), currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(await RaffleResponse.FromAsync(result.Value, companyDirectory, contactNames, operators, cancellationToken));
    }

    private static async Task<IResult> ReopenRaffle(
        [FromRoute] Guid eventId,
        [FromServices] IRaffleService service,
        [FromServices] ICompanyDirectory companyDirectory,
        [FromServices] IContactNameDirectory contactNames,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var result = await service.ReopenAsync(eventId, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(await RaffleResponse.FromAsync(result.Value, companyDirectory, contactNames, operators, cancellationToken));
    }

    private static async Task<IResult> UpdateRaffle(
        [FromRoute] Guid eventId,
        [FromBody] UpdateRaffleRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(MapToUpdateDto(eventId, request, currentUser.Id), cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.NoContent();
    }

    private static async Task<IResult> SetCertificateSigners(
        [FromRoute] Guid eventId,
        [FromBody] SetCertificateSignersRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.SetCertificateSignersAsync(
            eventId, request.ToCertificateSigners(), currentUser.Id, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private static async Task<IResult> SetAttendeeOverride(
        [FromRoute] Guid eventId,
        [FromBody] SetAttendeeOverrideRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var dto = new SetAttendeeOverrideDto(
            EventId: eventId,
            IdentityKey: request.IdentityKey ?? request.Email!,
            Decision: request.Decision!,
            Note: string.IsNullOrWhiteSpace(request.Note) ? null : request.Note,
            DecidedById: currentUser.Id);

        var result = await service.SetOverrideAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.NoContent();
    }

    private static async Task<IResult> BulkExcludeAttendees(
        [FromRoute] Guid eventId,
        [FromBody] BulkExcludeAttendeesRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var dto = new BulkExcludeAttendeesDto(
            EventId: eventId,
            SourceEventId: request.SourceEventId,
            Identifiers: request.Identifiers,
            Note: string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            DecidedById: currentUser.Id);

        var result = await service.BulkExcludeAsync(dto, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem()
            : Results.Ok(BulkExcludeAttendeesResponse.From(result.Value));
    }

    private static async Task<IResult> RemoveAttendeeOverride(
        [FromRoute] Guid eventId,
        [FromBody] RemoveAttendeeOverrideRequest request,
        [FromServices] IRaffleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RemoveOverrideAsync(eventId, request.IdentityKey ?? request.Email!, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.NoContent();
    }

    private static async Task<IResult> ReinstateAttendee(
        [FromRoute] Guid eventId,
        [FromRoute] Guid attendeeId,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICompanyDirectory companyDirectory,
        [FromServices] IContactNameDirectory contactNames,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var result = await service.ReinstateAttendeeAsync(eventId, attendeeId, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(await RaffleResponse.FromAsync(result.Value, companyDirectory, contactNames, operators, cancellationToken));
    }

    private static async Task<IResult> SetDisplayFields(
        [FromRoute] Guid eventId,
        [FromBody] SetRaffleDisplayFieldsRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] ICompanyDirectory companyDirectory,
        [FromServices] IContactNameDirectory contactNames,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        if (!RaffleDisplayFields.IsValidName(request.Name)
            || !RaffleDisplayFields.IsValidIdentifier(request.Identifier)
            || !RaffleDisplayFields.IsValidDetail(request.Detail))
        {
            return new BaseError(
                "Los campos visibles del participante deben usar valores válidos.",
                "RAFFLE_DISPLAY_FIELDS_INVALID",
                ErrorKind.Invalid).ToProblem();
        }

        var fields = new RaffleDisplayFields(request.Name!, request.Identifier!, request.Detail!);
        var result = await service.SetDisplayFieldsAsync(eventId, fields, currentUser.Id, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(await RaffleResponse.FromAsync(result.Value, companyDirectory, contactNames, operators, cancellationToken));
    }

    // ----- Mapping -----

    private static UpdateRaffleDto MapToUpdateDto(Guid eventId, UpdateRaffleRequest request, Guid actorId)
    {
        return new UpdateRaffleDto(
            EventId: eventId,
            VisualStyle: request.VisualStyle!,
            ShowPrizeOnScreen: request.ShowPrizeOnScreen!.Value,
            AnimationSeconds: request.AnimationSeconds!.Value,
            RequiresPresenceConfirmation: request.RequiresPresenceConfirmation!.Value,
            OnePrizePerPerson: request.OnePrizePerPerson!.Value,
            OnePrizePerCompany: (request.OnePrizePerCompany ?? request.OnePrizePerDepartmentPerRound)!.Value,
            Authorities: new RaffleAuthorities(
                request.NotaryName,
                request.NotaryIdentityDocument,
                request.WitnessOneName,
                request.WitnessOneIdentityDocument,
                request.WitnessTwoName,
                request.WitnessTwoIdentityDocument),
            PickupInstructions: string.IsNullOrWhiteSpace(request.PickupInstructions)
                ? null
                : request.PickupInstructions.Trim(),
            UpdatedById: actorId,
            RequiresCheckIn: request.RequiresCheckIn,
            BoardTitle: string.IsNullOrWhiteSpace(request.BoardTitle) ? null : request.BoardTitle.Trim(),
            BoardSubtitle: string.IsNullOrWhiteSpace(request.BoardSubtitle) ? null : request.BoardSubtitle.Trim(),
            BoardIdentifier: string.IsNullOrWhiteSpace(request.BoardIdentifier) ? null : request.BoardIdentifier.Trim(),
            BoardBatchSize: request.BoardBatchSize,
            LinkedEventId: request.LinkedEventId);
    }
}

public sealed record SetRaffleDisplayFieldsRequest(
    string? Name,
    string? Identifier,
    string? Detail);
