using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.Validators;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// El sorteo de un premio, su anulación, su entrega y su bombo. Un grupo propio y no el de
/// <see cref="PrizeEndpoints"/>: la política de grupo de aquel, <c>raffles:write</c>, se sumaría a la
/// de cada endpoint, y sortear, anular o entregar exige <c>raffles:draw</c> y nada más. La lectura del bombo no tiene política propia y la cubre la
/// fallback policy, como toda lectura del sorteo.
/// </summary>
public sealed class PrizeDrawEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // El servicio del sorteo lo compone DomainServicesConfiguration.
        services.AddScoped<IValidator<VoidPrizeDrawRequest>, VoidPrizeDrawRequestValidator>();
        services.AddScoped<IValidator<HandOverPrizeRequest>, HandOverPrizeRequestValidator>();
        services.AddScoped<IValidator<ReversePrizeHandoverRequest>, ReversePrizeHandoverRequestValidator>();
        services.AddScoped<IValidator<DrawBatchPrizesRequest>, DrawBatchPrizesRequestValidator>();
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/events/{eventId:guid}/raffle/prizes/{prizeId:guid}")
            .WithApiVersionSet(versionSet)
            .WithTags("Raffles")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Sin cuerpo, así que no hay validación; el 200 se declara igual que en el resto.
        group.MapPost("draw", DrawPrize)
            .MapToApiVersion(1)
            .WithName("DrawPrize")
            .WithSummary(RaffleEndpointsDescriptions.DrawPrizeSummary)
            .WithDescription(RaffleEndpointsDescriptions.DrawPrizeDescription)
            .RequireAuthorization(AuthorizationPolicies.RafflesDraw)
            .Produces<PrizeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // WithValidation quita el 200 inferido, así que se declara.
        group.MapPost("void", VoidPrizeDraw)
            .MapToApiVersion(1)
            .WithName("VoidPrizeDraw")
            .WithSummary(RaffleEndpointsDescriptions.VoidPrizeDrawSummary)
            .WithDescription(RaffleEndpointsDescriptions.VoidPrizeDrawDescription)
            .WithValidation<VoidPrizeDrawRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesDraw)
            .Produces<PrizeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Un formulario multipart: los campos de quien retira y la firma en una sola llamada (ADR-0014).
        group.MapPost("handover", HandOverPrize)
            .MapToApiVersion(1)
            .WithName("HandOverPrize")
            .WithSummary(RaffleEndpointsDescriptions.HandOverPrizeSummary)
            .WithDescription(RaffleEndpointsDescriptions.HandOverPrizeDescription)
            // La API se autentica con un bearer y no con una cookie, así que no hay petición
            // que un tercero pueda montar en nombre de nadie.
            .DisableAntiforgery()
            .Accepts<HandOverPrizeRequest>("multipart/form-data")
            .WithValidation<HandOverPrizeRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesDraw)
            .Produces<PrizeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("handover/reverse", ReversePrizeHandover)
            .MapToApiVersion(1)
            .WithName("ReversePrizeHandover")
            .WithSummary(RaffleEndpointsDescriptions.ReversePrizeHandoverSummary)
            .WithDescription(RaffleEndpointsDescriptions.ReversePrizeHandoverDescription)
            .WithValidation<ReversePrizeHandoverRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesDraw)
            .Produces<PrizeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Sin policy: la fallback policy exige, eso sí, un usuario autenticado.
        group.MapGet("eligible", GetEligible)
            .MapToApiVersion(1)
            .WithName("GetPrizeEligible")
            .WithSummary(RaffleEndpointsDescriptions.GetEligibleSummary)
            .WithDescription(RaffleEndpointsDescriptions.GetEligibleDescription)
            .Produces<EligibleResponse>();

        // A33: sorteo por lote (hasta 10 premios en una sola transacción).
        app.MapPost("v{version:apiVersion}/events/{eventId:guid}/raffle/prizes/draw-batch", DrawBatchPrizes)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Raffles")
            .WithName("DrawBatchPrizes")
            .WithSummary("Sortear un lote de hasta 10 premios en una transacción")
            .WithDescription("Sortea hasta 10 premios del sorteo en orden dentro de una sola transacción, respetando las reglas de exclusión entre premios del lote. Si el bombo se agota a mitad, persiste los sorteados hasta ese punto y devuelve 409 RAFFLE_POOL_EXHAUSTED con extensions.drawn.")
            .WithValidation<DrawBatchPrizesRequest>()
            .RequireAuthorization(AuthorizationPolicies.RafflesDraw)
            .Produces<IReadOnlyList<PrizeResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> DrawPrize(
        [FromRoute] Guid eventId,
        [FromRoute] Guid prizeId,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var result = await service.DrawAsync(eventId, prizeId, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var operatorNames = await RaffleResponse.ResolveOperatorNamesAsync(result.Value.Draws, operators, cancellationToken);
        return Results.Ok(PrizeResponse.From(result.Value, operatorNames));
    }

    private static async Task<IResult> VoidPrizeDraw(
        [FromRoute] Guid eventId,
        [FromRoute] Guid prizeId,
        [FromBody] VoidPrizeDrawRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var dto = new VoidPrizeDrawDto(
            EventId: eventId,
            PrizeId: prizeId,
            Code: request.Code!,
            Note: string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            VoidedById: currentUser.Id);

        var result = await service.VoidAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var operatorNames = await RaffleResponse.ResolveOperatorNamesAsync(result.Value.Draws, operators, cancellationToken);
        return Results.Ok(PrizeResponse.From(result.Value, operatorNames));
    }

    private static async Task<IResult> HandOverPrize(
        [FromRoute] Guid eventId,
        [FromRoute] Guid prizeId,
        [FromForm] HandOverPrizeRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var signature = request.Signature!;
        HandOverPrizeRequestValidator.TryParseIsWinner(request.ReceiverIsWinner, out var isWinner);

        await using var content = signature.OpenReadStream();

        // Algunos clientes mandan la ruta completa como nombre; solo interesa el último tramo. Un
        // blob de lienzo sin nombre se guarda con uno genérico.
        var fileName = Path.GetFileName(signature.FileName);

        var dto = new HandOverPrizeDto(
            EventId: eventId,
            PrizeId: prizeId,
            Receiver: new PrizeReceiver(request.ReceiverName!.Trim(), request.ReceiverIdentityDocument!.Trim(), isWinner),
            Signature: content,
            SignatureFileName: string.IsNullOrWhiteSpace(fileName) ? "firma.png" : fileName,
            SignatureContentType: string.IsNullOrWhiteSpace(signature.ContentType)
                ? FileSignatures.UndeclaredContentType
                : signature.ContentType,
            RedeemedById: currentUser.Id);

        var result = await service.HandOverAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var operatorNames = await RaffleResponse.ResolveOperatorNamesAsync(result.Value.Draws, operators, cancellationToken);
        return Results.Ok(PrizeResponse.From(result.Value, operatorNames));
    }

    private static async Task<IResult> ReversePrizeHandover(
        [FromRoute] Guid eventId,
        [FromRoute] Guid prizeId,
        [FromBody] ReversePrizeHandoverRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var dto = new ReversePrizeHandoverDto(
            EventId: eventId,
            PrizeId: prizeId,
            Reason: request.Reason!.Trim(),
            ReversedById: currentUser.Id);

        var result = await service.ReverseHandoverAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var operatorNames = await RaffleResponse.ResolveOperatorNamesAsync(result.Value.Draws, operators, cancellationToken);
        return Results.Ok(PrizeResponse.From(result.Value, operatorNames));
    }

    private static async Task<IResult> DrawBatchPrizes(
        [FromRoute] Guid eventId,
        [FromBody] DrawBatchPrizesRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        [FromServices] IRaffleOperatorDirectory operators,
        CancellationToken cancellationToken)
    {
        var result = await service.DrawBatchAsync(eventId, request.PrizeIds, currentUser.Id, cancellationToken);
        if (result.IsFailure)
            return result.Error.ToProblem();

        var allDraws = result.Value.DrawnPrizes.SelectMany(p => p.Draws).ToList();
        var operatorNames = await RaffleResponse.ResolveOperatorNamesAsync(allDraws, operators, cancellationToken);
        var responses = result.Value.DrawnPrizes
            .Select(p => PrizeResponse.From(p, operatorNames))
            .ToList();

        if (result.Value.ExhaustedError is { } exhausted)
        {
            var errorWithDrawn = exhausted with { Drawn = responses.Cast<object>().ToList() };
            return errorWithDrawn.ToProblem();
        }

        return Results.Ok(responses);
    }

    private static async Task<IResult> GetEligible(
        [FromRoute] Guid eventId,
        [FromRoute] Guid prizeId,
        [FromQuery] int? limit,
        [FromServices] IRaffleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetEligibleAsync(eventId, prizeId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.Ok(EligibleResponse.From(result.Value, limit));
    }
}
