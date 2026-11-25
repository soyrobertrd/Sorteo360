using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Endpoints persistidos del padrón de participantes, check-in QR, gafetes PDF y webhooks
/// autenticados por clave y firma HMAC.
/// </summary>
public sealed class RaffleParticipantWebhookEndpoints : IEndpoint
{
    private static readonly JsonSerializerOptions BodyOptions = new() { PropertyNameCaseInsensitive = true };

    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Las dependencias de dominio e infraestructura se registran en DomainServicesConfiguration.
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
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("participants", ListParticipants)
            .MapToApiVersion(1)
            .WithName("ListRaffleParticipants")
            .WithSummary(RaffleParticipantEndpointsDescriptions.ListSummary)
            .Produces<IReadOnlyList<RaffleParticipantDto>>();

        group.MapPost("participants/import", ImportParticipants)
            .MapToApiVersion(1)
            .WithName("ImportRaffleParticipants")
            .WithSummary(RaffleParticipantEndpointsDescriptions.ImportSummary)
            .WithDescription(RaffleParticipantEndpointsDescriptions.ImportDescription)
            .WithMetadata(new RequestSizeLimitAttribute(20 * 1024 * 1024))
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces<ImportRaffleParticipantsHttpResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("participants/check-in", CheckInParticipant)
            .MapToApiVersion(1)
            .WithName("SetRaffleParticipantCheckIn")
            .WithSummary(RaffleParticipantEndpointsDescriptions.CheckInSummary)
            .WithDescription(RaffleParticipantEndpointsDescriptions.CheckInDescription)
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces<RaffleParticipantDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("participants/check-in-bulk", BulkCheckInParticipants)
            .MapToApiVersion(1)
            .WithName("BulkRaffleParticipantCheckIn")
            .WithSummary(RaffleParticipantEndpointsDescriptions.BulkCheckInSummary)
            .WithDescription(RaffleParticipantEndpointsDescriptions.BulkCheckInDescription)
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("participants/remove", RemoveParticipantByBody)
            .MapToApiVersion(1)
            .WithName("RemoveRaffleParticipantByBody")
            .WithSummary(RaffleParticipantEndpointsDescriptions.RemoveSummary)
            .WithDescription(RaffleParticipantEndpointsDescriptions.RemoveDescription)
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("participants", RemoveParticipantByQuery)
            .MapToApiVersion(1)
            .WithName("RemoveRaffleParticipant")
            .WithSummary(RaffleParticipantEndpointsDescriptions.RemoveSummary)
            .WithDescription(RaffleParticipantEndpointsDescriptions.RemoveDescription)
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("participants/{code}/qr", GetParticipantQrPng)
            .MapToApiVersion(1)
            .WithName("GetRaffleParticipantQr")
            .WithSummary(RaffleParticipantEndpointsDescriptions.QrPngSummary)
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("participants/badges.pdf", GetParticipantsBadgesPdf)
            .MapToApiVersion(1)
            .WithName("GetRaffleParticipantsBadgesPdf")
            .WithSummary(RaffleParticipantEndpointsDescriptions.BadgesPdfSummary)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("audit", GetRaffleAudit)
            .MapToApiVersion(1)
            .WithName("GetRaffleAudit")
            .WithSummary(RaffleParticipantEndpointsDescriptions.AuditSummary)
            .WithDescription(RaffleParticipantEndpointsDescriptions.AuditDescription)
            .Produces<RaffleAuditSummaryHttpResponse>(StatusCodes.Status200OK);

        group.MapPost("webhooks/participants", WebhookImportParticipants)
            .MapToApiVersion(1)
            .WithName("WebhookImportRaffleParticipants")
            .WithSummary(RaffleParticipantEndpointsDescriptions.WebhookImportSummary)
            .WithDescription(RaffleParticipantEndpointsDescriptions.WebhookImportDescription)
            .AllowAnonymous()
            .Produces<ImportRaffleParticipantsHttpResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("webhooks/check-in", WebhookCheckInParticipant)
            .MapToApiVersion(1)
            .WithName("WebhookCheckInRaffleParticipant")
            .WithSummary(RaffleParticipantEndpointsDescriptions.WebhookCheckInSummary)
            .WithDescription(RaffleParticipantEndpointsDescriptions.WebhookCheckInDescription)
            .AllowAnonymous()
            .Produces<RaffleParticipantDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListParticipants(
        [FromRoute] Guid eventId,
        [FromServices] IRaffleParticipantRepository participants,
        [FromServices] IRaffleRepository raffles,
        CancellationToken cancellationToken)
    {
        var listResult = await participants.ListByEventIdAsync(eventId, cancellationToken);
        if (listResult.IsFailure)
            return listResult.Error.ToProblem();

        var context = await LoadRaffleContextAsync(eventId, raffles, cancellationToken);
        return Results.Ok(listResult.Value.Select(p => MapToDto(p, context)).ToList());
    }

    private static async Task<IResult> ImportParticipants(
        [FromRoute] Guid eventId,
        [FromBody] ImportRaffleParticipantsHttpRequest request,
        [FromServices] IRaffleParticipantRepository participants,
        [FromServices] IRaffleRepository raffles,
        [FromServices] IPlanLimitGuard planLimits,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var inputs = (request.Participants ?? [])
            .Select(p => new RaffleParticipantInput(
                string.IsNullOrWhiteSpace(p.FullName)
                    ? $"{p.FirstName} {p.LastName}".Trim()
                    : p.FullName,
                p.Email,
                p.Company,
                p.Department,
                p.EmployeeCode,
                p.Phone,
                p.QrCode,
                p.CheckedIn,
                p.FirstName,
                p.LastName,
                p.Location))
            .ToList();

        var capError = await EnforceParticipantPlanLimitAsync(
            eventId, inputs, request.ReplaceExisting ?? false, participants, planLimits, cancellationToken);
        if (capError is not null)
            return capError;

        var importResult = await participants.ImportAsync(
            eventId,
            inputs,
            replaceExisting: request.ReplaceExisting ?? false,
            markCheckedIn: request.MarkCheckedIn ?? false,
            source: request.Source ?? "manual",
            now: clock.GetUtcNow(),
            cancellationToken: cancellationToken);

        if (importResult.IsFailure)
            return importResult.Error.ToProblem();

        var context = await LoadRaffleContextAsync(eventId, raffles, cancellationToken);
        var res = importResult.Value;
        return Results.Ok(new ImportRaffleParticipantsHttpResponse(
            res.Imported,
            res.Updated,
            res.Total,
            res.Participants.Select(p => MapToDto(p, context)).ToList(),
            res.Rejected ?? []));
    }

    private static async Task<IResult> CheckInParticipant(
        [FromRoute] Guid eventId,
        [FromBody] CheckInRaffleParticipantHttpRequest request,
        [FromServices] IRaffleParticipantRepository participants,
        [FromServices] IRaffleRepository raffles,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var lookup = request.Code ?? request.RegistrationIdOrCode ?? request.QrCode ?? request.Email ?? request.EmployeeCode ?? string.Empty;
        var checkInResult = await participants.SetCheckInAsync(
            eventId,
            lookup,
            request.CheckedIn ?? true,
            clock.GetUtcNow(),
            cancellationToken);

        if (checkInResult.IsFailure)
            return checkInResult.Error.ToProblem();

        if (checkInResult.Value.HasNoValue)
            return new BaseError("Participante no encontrado para check-in.", "REGISTRATION_NOT_FOUND", ErrorKind.NotFound).ToProblem();

        var context = await LoadRaffleContextAsync(eventId, raffles, cancellationToken);
        return Results.Ok(MapToDto(checkInResult.Value.Value, context));
    }

    private static async Task<IResult> BulkCheckInParticipants(
        [FromRoute] Guid eventId,
        [FromBody] BulkCheckInHttpRequest request,
        [FromServices] IRaffleParticipantRepository participants,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var result = await participants.BulkSetCheckInAsync(
            eventId,
            request.CheckedIn,
            clock.GetUtcNow(),
            cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private static async Task<IResult> RemoveParticipantByBody(
        [FromRoute] Guid eventId,
        [FromBody] RemoveRaffleParticipantHttpRequest request,
        [FromServices] IRaffleParticipantRepository participants,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.NoContent();

        var result = await participants.RemoveAsync(eventId, request.Email, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private static async Task<IResult> RemoveParticipantByQuery(
        [FromRoute] Guid eventId,
        [FromQuery] string? email,
        [FromServices] IRaffleParticipantRepository participants,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Results.NoContent();

        var result = await participants.RemoveAsync(eventId, email, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private static async Task<IResult> GetParticipantQrPng(
        [FromRoute] Guid eventId,
        [FromRoute] string code,
        [FromServices] IRaffleParticipantRepository participants,
        [FromServices] IRaffleBadgeRenderer renderer,
        CancellationToken cancellationToken)
    {
        var listResult = await participants.ListByEventIdAsync(eventId, cancellationToken);
        if (listResult.IsFailure)
            return listResult.Error.ToProblem();

        var norm = code.Trim();
        var match = listResult.Value.FirstOrDefault(p =>
            string.Equals(p.QrCode, norm, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Id.ToString(), norm, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(p.EmployeeCode)
                && string.Equals(p.EmployeeCode.Trim(), norm, StringComparison.OrdinalIgnoreCase))
            || string.Equals(p.Email, norm.ToLowerInvariant(), StringComparison.Ordinal));

        if (match is null)
            return new BaseError("Participante no encontrado.", "REGISTRATION_NOT_FOUND", ErrorKind.NotFound).ToProblem();

        var pngBytes = renderer.RenderQrPng(match.QrCode);
        return Results.File(pngBytes, "image/png", $"{match.QrCode}.png");
    }

    private static async Task<IResult> GetParticipantsBadgesPdf(
        [FromRoute] Guid eventId,
        [FromServices] IRaffleParticipantRepository participants,
        [FromServices] IEventRepository events,
        [FromServices] IRaffleBadgeRenderer renderer,
        CancellationToken cancellationToken)
    {
        var listResult = await participants.ListByEventIdAsync(eventId, cancellationToken);
        if (listResult.IsFailure)
            return listResult.Error.ToProblem();

        var eventName = "Sorteo";
        var eventResult = await events.GetByIdAsync(eventId, cancellationToken);
        if (eventResult.IsSuccess && eventResult.Value.HasValue)
            eventName = eventResult.Value.Value.Event.Name;

        var pdfBytes = renderer.RenderBadgesPdf(eventName, listResult.Value);
        return Results.File(pdfBytes, "application/pdf", $"gafetes-{eventId}.pdf");
    }

    private static async Task<IResult> GetRaffleAudit(
        [FromRoute] Guid eventId,
        [FromServices] IRaffleParticipantRepository participants,
        [FromServices] IRaffleRepository raffles,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var listResult = await participants.ListByEventIdAsync(eventId, cancellationToken);
        if (listResult.IsFailure)
            return listResult.Error.ToProblem();

        var context = await LoadRaffleContextAsync(eventId, raffles, cancellationToken);
        var mapped = listResult.Value.Select(p => MapToDto(p, context)).ToList();
        var eligible = mapped.Where(p => p.Qualified && p.ExclusionReason is null).ToList();

        var prizesById = context.RaffleWithRounds?.Rounds
            .SelectMany(r => r.Prizes.Select(p => (Prize: p, Round: r)))
            .ToDictionary(x => x.Prize.Id)
            ?? [];

        var orderedDraws = (context.RaffleWithRounds?.Draws ?? [])
            .OrderBy(d => d.DrawnAt)
            .ThenBy(d => d.Id)
            .ToList();

        var drumHash = ComputeDrumHash(eligible);
        var drawChainHash = ComputeDrawChainHash(drumHash, orderedDraws);

        var entries = orderedDraws.Select(draw =>
        {
            prizesById.TryGetValue(draw.PrizeId, out var prizeInfo);
            return new RaffleAuditEntryHttpResponse(
                DrawId: draw.Id,
                PrizeId: draw.PrizeId,
                PrizeName: prizeInfo.Prize?.Name ?? string.Empty,
                RoundNumber: prizeInfo.Round?.Number ?? 0,
                RoundName: prizeInfo.Round?.Name ?? string.Empty,
                WinnerAttendeeId: draw.WinnerAttendeeId,
                WinnerName: draw.WinnerName,
                WinnerCompany: draw.WinnerCompany,
                DrawnAt: draw.DrawnAt,
                EligiblePoolSize: draw.EligiblePoolSize,
                RedemptionCode: draw.RedemptionCode,
                VoidCode: draw.VoidCode,
                VoidedAt: draw.VoidedAt);
        }).ToList();

        return Results.Ok(new RaffleAuditSummaryHttpResponse(
            EventId: eventId,
            RaffleState: context.RaffleWithRounds?.Raffle.State ?? RaffleState.Draft,
            DrumHash: drumHash,
            DrawChainHash: drawChainHash,
            TotalEligible: eligible.Count,
            TotalDraws: orderedDraws.Count,
            GeneratedAt: clock.GetUtcNow(),
            Entries: entries));
    }

    private static async Task<IResult> WebhookImportParticipants(
        HttpContext httpContext,
        [FromRoute] Guid eventId,
        [FromServices] IRaffleParticipantRepository participants,
        [FromServices] IRaffleRepository raffles,
        [FromServices] IPlanLimitGuard planLimits,
        [FromServices] IConfiguration configuration,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var verified = await VerifyAndReadWebhookBodyAsync(
            httpContext, eventId, participants, planLimits, configuration, clock, cancellationToken);
        if (verified.ErrorResult is not null)
            return verified.ErrorResult;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(verified.Body!);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        using (document)
        {
            var items = ExtractWebhookParticipants(document.RootElement);
            if (items.Count > 5_000)
            {
                return Results.Problem(
                    title: "Lote de webhook demasiado grande",
                    detail: "El webhook acepta como máximo 5.000 participantes por petición.",
                    statusCode: StatusCodes.Status413PayloadTooLarge,
                    extensions: new Dictionary<string, object?> { ["code"] = "WEBHOOK_BATCH_TOO_LARGE", ["max"] = 5_000 });
            }

            var capError = await EnforceParticipantPlanLimitAsync(
                eventId, items, replaceExisting: false, participants, planLimits, cancellationToken, bypassAccountFilter: true);
            if (capError is not null)
                return capError;

            var importResult = await participants.ImportAsync(
                eventId,
                items,
                replaceExisting: false,
                markCheckedIn: false,
                source: "webhook",
                now: clock.GetUtcNow(),
                cancellationToken: cancellationToken,
                bypassAccountFilter: true);

            if (importResult.IsFailure)
                return importResult.Error.ToProblem();

            var context = await LoadRaffleContextAsync(eventId, raffles, cancellationToken);
            var res = importResult.Value;
            return Results.Ok(new ImportRaffleParticipantsHttpResponse(
                res.Imported,
                res.Updated,
                res.Total,
                res.Participants.Select(p => MapToDto(p, context)).ToList(),
                res.Rejected ?? []));
        }
    }

    private static async Task<IResult> WebhookCheckInParticipant(
        HttpContext httpContext,
        [FromRoute] Guid eventId,
        [FromServices] IRaffleParticipantRepository participants,
        [FromServices] IRaffleRepository raffles,
        [FromServices] IPlanLimitGuard planLimits,
        [FromServices] IConfiguration configuration,
        [FromServices] TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var verified = await VerifyAndReadWebhookBodyAsync(
            httpContext, eventId, participants, planLimits, configuration, clock, cancellationToken);
        if (verified.ErrorResult is not null)
            return verified.ErrorResult;

        CheckInRaffleParticipantHttpRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<CheckInRaffleParticipantHttpRequest>(verified.Body!, BodyOptions);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (request is null)
            return Results.BadRequest();

        var lookup = request.Code ?? request.RegistrationIdOrCode ?? request.QrCode ?? request.Email ?? request.EmployeeCode ?? string.Empty;
        var checkInResult = await participants.SetCheckInAsync(
            eventId,
            lookup,
            request.CheckedIn ?? true,
            clock.GetUtcNow(),
            cancellationToken,
            bypassAccountFilter: true);

        if (checkInResult.IsFailure)
            return checkInResult.Error.ToProblem();

        if (checkInResult.Value.HasNoValue)
            return new BaseError("Participante no encontrado para check-in.", "REGISTRATION_NOT_FOUND", ErrorKind.NotFound).ToProblem();

        var context = await LoadRaffleContextAsync(eventId, raffles, cancellationToken);
        return Results.Ok(MapToDto(checkInResult.Value.Value, context));
    }

    private static async Task<(byte[]? Body, IResult? ErrorResult)> VerifyAndReadWebhookBodyAsync(
        HttpContext httpContext,
        Guid eventId,
        IRaffleParticipantRepository participants,
        IPlanLimitGuard planLimits,
        IConfiguration configuration,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // R5: resolver cuenta y clave propia del evento (sin depender del usuario actual).
        var accessResult = await participants.ResolveEventWebhookAsync(eventId, cancellationToken);
        if (accessResult.IsFailure)
            return (null, accessResult.Error.ToProblem());

        if (accessResult.Value.HasNoValue)
            return (null, Sorteo360.Events.Core.Errors.EventImageErrors.EventNotFound().ToProblem());

        var access = accessResult.Value.Value;

        var apiFeatureCheck = await planLimits.EnsureFeatureEnabledAsync(access.AccountId, "api", cancellationToken);
        if (apiFeatureCheck.IsFailure)
            return (null, apiFeatureCheck.Error.ToProblem());

        // Clave por evento primero; clave global solo como respaldo heredado para Banreservas si el evento no tiene clave propia.
        var expectedKey = !string.IsNullOrWhiteSpace(access.WebhookSecret)
            ? access.WebhookSecret
            : access.AccountId == Account.DefaultBanreservasAccountId
                ? RaffleWebhookSignature.ResolveApiKey(configuration, eventId)
                : null;

        var presentedKey = RaffleWebhookSignature.ReadPresentedKey(httpContext.Request.Headers);
        if (!RaffleWebhookSignature.KeyMatches(expectedKey, presentedKey))
            return (null, Results.StatusCode(StatusCodes.Status401Unauthorized));

        if (httpContext.Request.ContentLength > RaffleWebhookSignature.MaxBodyBytes)
            return (null, Results.StatusCode(StatusCodes.Status413PayloadTooLarge));

        var body = await ReadBodyAsync(httpContext.Request.Body, cancellationToken);
        if (body is null)
            return (null, Results.StatusCode(StatusCodes.Status413PayloadTooLarge));

        var signingSecret = !string.IsNullOrWhiteSpace(access.WebhookSecret)
            ? null
            : access.AccountId == Account.DefaultBanreservasAccountId
                ? RaffleWebhookSignature.ResolveSigningSecret(configuration, eventId)
                : null;
        if (!string.IsNullOrWhiteSpace(signingSecret))
        {
            var signatureHeader = RaffleWebhookSignature.ReadPresentedSignature(httpContext.Request.Headers);
            if (!RaffleWebhookSignature.SignatureMatches(signingSecret, signatureHeader, body, clock.GetUtcNow()))
                return (null, Results.StatusCode(StatusCodes.Status401Unauthorized));
        }

        return (body, null);
    }

    private static async Task<byte[]?> ReadBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[RaffleWebhookSignature.MaxBodyBytes + 1];
        var read = 0;

        while (read < buffer.Length)
        {
            var chunk = await body.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (chunk == 0)
                return buffer[..read];
            read += chunk;
        }

        return null;
    }

    private static async Task<RaffleParticipantContext> LoadRaffleContextAsync(
        Guid eventId,
        IRaffleRepository raffles,
        CancellationToken cancellationToken)
    {
        var raffleResult = await raffles.GetWithRoundsByEventIdAsync(eventId, cancellationToken);
        var withRounds = raffleResult.IsSuccess && raffleResult.Value.HasValue
            ? raffleResult.Value.Value
            : null;

        Dictionary<string, string> overrides = new(StringComparer.Ordinal);
        if (withRounds is not null)
        {
            var overridesResult = await raffles.ListOverridesAsync(withRounds.Raffle.Id, cancellationToken);
            if (overridesResult.IsSuccess)
            {
                overrides = overridesResult.Value.ToDictionary(
                    o => o.IdentityKey,
                    o => o.Decision,
                    StringComparer.Ordinal);
            }
        }

        var liveDraws = (withRounds?.Draws ?? []).Where(d => d.IsLive).ToList();
        var winnerKeys = liveDraws
            .Select(d => d.WinnerIdentityKey.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var winnerCompanyKeys = liveDraws
            .Select(d => CompanyKey.Normalize(d.WinnerCompany))
            .Where(k => k.HasValue)
            .Select(k => k.Value)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlySet<string> linkedWinnerKeys = new HashSet<string>(StringComparer.Ordinal);
        if (withRounds is not null)
        {
            var linkedResult = await raffles.ListLinkedWinnerKeysAsync(withRounds.Raffle, cancellationToken);
            if (linkedResult.IsSuccess)
                linkedWinnerKeys = linkedResult.Value;
        }

        return new RaffleParticipantContext(withRounds, overrides, winnerKeys, winnerCompanyKeys, linkedWinnerKeys);
    }

    internal static RaffleParticipantDto MapToDto(
        RaffleParticipant participant,
        RaffleParticipantContext context)
    {
        context.Overrides.TryGetValue(participant.IdentityKey, out var manualDecision);
        var exclusionReason = ResolveExclusionReason(participant, manualDecision, context);

        var qualified = manualDecision switch
        {
            AttendeeOverrideDecision.Excluded => false,
            AttendeeOverrideDecision.Included => true,
            _ => exclusionReason is null,
        };

        return new RaffleParticipantDto
        {
            Id = participant.Id.ToString(),
            FullName = participant.FullName,
            FirstName = participant.FirstName,
            LastName = participant.LastName,
            Email = participant.Email,
            IdentityKey = participant.IdentityKey,
            Company = participant.Company,
            Department = participant.Department,
            Location = participant.Location,
            EmployeeCode = participant.EmployeeCode,
            Phone = participant.Phone,
            QrCode = participant.QrCode,
            TicketNumber = string.IsNullOrWhiteSpace(participant.TicketNumber) ? null : participant.TicketNumber,
            CheckedIn = participant.CheckedIn,
            CheckedInAt = participant.CheckedInAt?.ToString("O"),
            Source = participant.Source,
            Qualified = qualified,
            Override = manualDecision,
            ExclusionReason = exclusionReason,
        };
    }

    internal static string? ResolveExclusionReason(
        RaffleParticipant participant,
        string? manualDecision,
        RaffleParticipantContext context)
    {
        if (manualDecision == AttendeeOverrideDecision.Excluded)
            return "manually_excluded";

        var raffle = context.RaffleWithRounds?.Raffle;
        var requiresCheckIn = raffle?.RequiresCheckIn ?? true;

        if (requiresCheckIn && !participant.CheckedIn && manualDecision != AttendeeOverrideDecision.Included)
            return "not_checked_in";

        if ((raffle?.OnePrizePerPerson ?? true)
            && context.LiveWinnerIdentityKeys.Contains(participant.IdentityKey))
        {
            return "already_won";
        }

        // Ganó en un sorteo vinculado (mismo evento): tampoco entra a este bombo.
        if (context.LinkedWinnerIdentityKeys?.Contains(participant.IdentityKey) == true)
            return "won_in_linked_raffle";

        if (raffle?.OnePrizePerCompany ?? false)
        {
            var participantKey = CompanyKey.Normalize(participant.RuleGroup);
            if (participantKey.HasValue && context.LiveWinnerCompanyKeys.Contains(participantKey.Value))
                return "same_department";
        }

        return null;
    }

    internal static string ComputeDrumHash(IReadOnlyList<RaffleParticipantDto> eligible)
    {
        var canonical = string.Join('\n', eligible
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ThenBy(p => p.Email, StringComparer.Ordinal)
            .ThenBy(p => p.EmployeeCode ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(p => p.FullName, StringComparer.Ordinal)
            .Select(p => $"{p.Id}|{p.Email}|{p.EmployeeCode ?? string.Empty}|{p.Department ?? p.Company ?? string.Empty}"));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    internal static string ComputeDrawChainHash(string initialHash, IReadOnlyList<PrizeDraw> orderedDraws)
    {
        var current = initialHash;
        foreach (var draw in orderedDraws)
        {
            var payload = $"{current}|{draw.Id:D}|{draw.PrizeId:D}|{draw.WinnerAttendeeId:D}|{draw.DrawnAt:O}|{draw.RedemptionCode}|{draw.VoidedAt:O}";
            current = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }

        return current;
    }

    internal sealed record RaffleParticipantContext(
        RaffleWithRounds? RaffleWithRounds,
        IReadOnlyDictionary<string, string> Overrides,
        IReadOnlySet<string> LiveWinnerIdentityKeys,
        IReadOnlySet<string> LiveWinnerCompanyKeys,
        IReadOnlySet<string>? LinkedWinnerIdentityKeys = null);

    private static async Task<IResult?> EnforceParticipantPlanLimitAsync(
        Guid eventId,
        IReadOnlyList<RaffleParticipantInput> incoming,
        bool replaceExisting,
        IRaffleParticipantRepository participants,
        IPlanLimitGuard planLimits,
        CancellationToken cancellationToken,
        bool bypassAccountFilter = false)
    {
        var validIncoming = incoming.Where(p => !string.IsNullOrWhiteSpace(p.FullName)).ToList();
        if (validIncoming.Count == 0)
            return null;

        var existingResult = await participants.ListByEventIdAsync(eventId, cancellationToken, bypassAccountFilter);
        if (existingResult.IsFailure)
            return existingResult.Error.ToProblem();

        var existing = replaceExisting ? [] : existingResult.Value;
        var existingCodes = existing
            .Where(p => !string.IsNullOrWhiteSpace(p.EmployeeCode))
            .Select(p => p.EmployeeCode!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEmails = existing
            .Where(p => p.Email is not null)
            .Select(p => p.Email!)
            .ToHashSet(StringComparer.Ordinal);

        var netNew = 0;
        foreach (var item in validIncoming)
        {
            var code = string.IsNullOrWhiteSpace(item.EmployeeCode) ? null : item.EmployeeCode.Trim();
            var email = string.IsNullOrWhiteSpace(item.Email) ? null : item.Email.Trim().ToLowerInvariant();
            var isExisting = (code is not null && existingCodes.Contains(code))
                || (email is not null && existingEmails.Contains(email));
            if (!isExisting)
            {
                netNew++;
                if (code is not null)
                    existingCodes.Add(code);
                if (email is not null)
                    existingEmails.Add(email);
            }
        }

        var resultingTotal = existing.Count + netNew;
        var check = await planLimits.CheckParticipantLimitAsync(eventId, resultingTotal, existing.Count, cancellationToken);
        if (check.IsFailure)
            return check.Error.ToProblem();

        if (!check.Value.Allowed && check.Value.Limit is { } limit)
        {
            var remaining = check.Value.Remaining ?? 0;
            var error = AccountErrors.PlanParticipantLimit(limit, remaining);
            return Results.Problem(
                title: "Límite de participantes del plan alcanzado",
                detail: error.Message,
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = error.Code,
                    ["limit"] = limit,
                    ["remaining"] = remaining,
                });
        }

        return null;
    }

    private static List<RaffleParticipantInput> ExtractWebhookParticipants(JsonElement root)
    {
        var list = new List<RaffleParticipantInput>();
        var arrayElement = root;

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (TryGetPropertyIgnoreCase(root, "participants", out var participantsProp) && participantsProp.ValueKind == JsonValueKind.Array)
                arrayElement = participantsProp;
            else if (TryGetPropertyIgnoreCase(root, "data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                arrayElement = dataProp;
            else if (TryGetPropertyIgnoreCase(root, "items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                arrayElement = itemsProp;
            else
            {
                var single = ParseParticipant(root);
                if (single is not null)
                    list.Add(single);
                return list;
            }
        }

        if (arrayElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var parsed = ParseParticipant(item);
            if (parsed is not null)
                list.Add(parsed);
        }

        return list;
    }

    private static RaffleParticipantInput? ParseParticipant(JsonElement obj)
    {
        var firstName = ReadString(obj, "firstName", "first_name", "nombres");
        var lastName = ReadString(obj, "lastName", "last_name", "apellido", "apellidos");
        var fullName = ReadString(obj, "fullName", "name", "nombre", "nombreCompleto")
            ?? (string.IsNullOrWhiteSpace($"{firstName} {lastName}") ? null : $"{firstName} {lastName}".Trim());
        var email = ReadString(obj, "email", "correo", "mail");
        var employeeCode = ReadString(obj, "employeeCode", "codigo", "code", "id");
        if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(employeeCode))
            return null;

        return new RaffleParticipantInput(
            FullName: fullName ?? email ?? employeeCode!,
            Email: email,
            Company: ReadString(obj, "company", "empresa", "organization"),
            Department: ReadString(obj, "department", "departamento", "area"),
            EmployeeCode: employeeCode,
            Phone: ReadString(obj, "phone", "telefono", "celular"),
            QrCode: ReadString(obj, "qrCode", "qr"),
            CheckedIn: ReadBool(obj, "checkedIn", "asistencia", "presente"),
            FirstName: firstName,
            LastName: lastName,
            Location: ReadString(obj, "location", "ubicacion", "sucursal", "localidad", "oficina"));
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? ReadString(JsonElement obj, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (TryGetPropertyIgnoreCase(obj, candidate, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var str = prop.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                    return str.Trim();
            }
        }
        return null;
    }

    private static bool? ReadBool(JsonElement obj, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!TryGetPropertyIgnoreCase(obj, candidate, out var prop))
                continue;
            if (prop.ValueKind == JsonValueKind.True)
                return true;
            if (prop.ValueKind == JsonValueKind.False)
                return false;
            if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var parsed))
                return parsed;
        }
        return null;
    }
}

public sealed record RaffleParticipantInputDto(
    string? FullName = null,
    string? Email = null,
    string? Company = null,
    string? Department = null,
    string? EmployeeCode = null,
    string? Phone = null,
    string? QrCode = null,
    bool? CheckedIn = null,
    string? FirstName = null,
    string? LastName = null,
    string? Location = null);

public sealed record ImportRaffleParticipantsHttpRequest(
    IReadOnlyList<RaffleParticipantInputDto>? Participants,
    bool? ReplaceExisting = null,
    bool? MarkCheckedIn = null,
    string? Source = null);

public sealed record CheckInRaffleParticipantHttpRequest(
    string? Code = null,
    string? RegistrationIdOrCode = null,
    string? QrCode = null,
    string? Email = null,
    string? EmployeeCode = null,
    bool? CheckedIn = true);

public sealed record BulkCheckInHttpRequest(bool CheckedIn);

public sealed record RemoveRaffleParticipantHttpRequest(string Email);

public sealed class RaffleParticipantDto
{
    public required string Id { get; init; }
    public required string FullName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    /// <summary>Nulo si la persona entró solo con su ID.</summary>
    public string? Email { get; init; }

    /// <summary>La clave interna de la persona en el evento: con ella se la excluye o incluye a mano.</summary>
    public required string IdentityKey { get; init; }
    public string? Company { get; init; }
    public string? Department { get; init; }
    public string? Location { get; init; }
    public string? EmployeeCode { get; init; }
    public string? Phone { get; init; }
    public required string QrCode { get; init; }
    public string? TicketNumber { get; init; }
    public bool CheckedIn { get; init; }
    public string? CheckedInAt { get; init; }
    public string Source { get; init; } = "manual";
    public bool Qualified { get; init; }
    public string? Override { get; init; }
    public string? ExclusionReason { get; init; }
}

public sealed record ImportRaffleParticipantsHttpResponse(
    int Imported,
    int Updated,
    int Total,
    IReadOnlyList<RaffleParticipantDto> Participants,
    IReadOnlyList<RaffleParticipantImportRejection> Rejected);

public sealed record RaffleAuditEntryHttpResponse(
    Guid DrawId,
    Guid PrizeId,
    string PrizeName,
    int RoundNumber,
    string RoundName,
    Guid WinnerAttendeeId,
    string WinnerName,
    string? WinnerCompany,
    DateTimeOffset DrawnAt,
    int EligiblePoolSize,
    string RedemptionCode,
    string? VoidCode,
    DateTimeOffset? VoidedAt);

public sealed record RaffleAuditSummaryHttpResponse(
    Guid EventId,
    string RaffleState,
    string DrumHash,
    string DrawChainHash,
    int TotalEligible,
    int TotalDraws,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RaffleAuditEntryHttpResponse> Entries);
