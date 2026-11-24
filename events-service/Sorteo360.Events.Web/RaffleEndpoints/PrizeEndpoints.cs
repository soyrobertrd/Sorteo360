using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Los premios del sorteo de un evento. Cuelgan del sorteo y no de la ronda en la ruta porque la
/// ronda es un campo más del premio: editarlo puede moverlo. Se leen con el sorteo; acá solo se
/// escriben, y todas las escrituras exigen <c>raffles:write</c>.
/// </summary>
public sealed class PrizeEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<CreatePrizesRequest>, Validators.CreatePrizesRequestValidator>();
        services.AddScoped<IValidator<ImportPrizesRequest>, Validators.ImportPrizesRequestValidator>();
        services.AddScoped<IValidator<UpdatePrizeRequest>, Validators.UpdatePrizeRequestValidator>();
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/events/{eventId:guid}/raffle/prizes")
            .WithApiVersionSet(versionSet)
            .WithTags("Raffles")
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("", CreatePrizes)
            .MapToApiVersion(1)
            .WithName("CreatePrizes")
            .WithSummary(RaffleEndpointsDescriptions.CreatePrizesSummary)
            .WithDescription(RaffleEndpointsDescriptions.CreatePrizesDescription)
            .WithValidation<CreatePrizesRequest>()
            .Produces<IReadOnlyList<PrizeResponse>>(StatusCodes.Status201Created);

        group.MapPost("import", ImportPrizes)
            .MapToApiVersion(1)
            .WithName("ImportPrizes")
            .WithSummary("Importa premios en bloque (hasta 10.000) creando las rondas que falten")
            .WithDescription("Crea en una sola transacción todo o nada las rondas faltantes y hasta 10.000 premios en un sorteo en borrador.")
            .WithValidation<ImportPrizesRequest>()
            .Produces<ImportPrizesResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapPatch("{prizeId:guid}", UpdatePrize)
            .MapToApiVersion(1)
            .WithName("UpdatePrize")
            .WithSummary(RaffleEndpointsDescriptions.UpdatePrizeSummary)
            .WithDescription(RaffleEndpointsDescriptions.UpdatePrizeDescription)
            .WithValidation<UpdatePrizeRequest>()
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("{prizeId:guid}", DeletePrize)
            .MapToApiVersion(1)
            .WithName("DeletePrize")
            .WithSummary(RaffleEndpointsDescriptions.DeletePrizeSummary)
            .WithDescription(RaffleEndpointsDescriptions.DeletePrizeDescription)
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("{prizeId:guid}/logo", UploadPrizeSponsorLogo)
            .MapToApiVersion(1)
            .WithName("UploadPrizeSponsorLogo")
            .WithSummary(RaffleEndpointsDescriptions.UploadPrizeSponsorLogoSummary)
            .WithDescription(RaffleEndpointsDescriptions.UploadPrizeSponsorLogoDescription)
            // El cuerpo es multipart y la petición viaja con Bearer, no con cookies: el token
            // antifalsificación no aplica y sin esto el filtro rechazaría toda subida.
            .DisableAntiforgery()
            .Produces<PrizeSponsorLogoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapDelete("{prizeId:guid}/logo", RemovePrizeSponsorLogo)
            .MapToApiVersion(1)
            .WithName("RemovePrizeSponsorLogo")
            .WithSummary(RaffleEndpointsDescriptions.RemovePrizeSponsorLogoSummary)
            .WithDescription(RaffleEndpointsDescriptions.RemovePrizeSponsorLogoDescription)
            .Produces(StatusCodes.Status204NoContent);

        // A34: foto del premio compartida por un grupo de premios iguales.
        group.MapPost("image", UploadPrizesImage)
            .MapToApiVersion(1)
            .WithName("UploadPrizesImage")
            .WithSummary("Sube la foto de un grupo de premios (JPG, PNG o WebP, máx. 5 MB)")
            .WithDescription("Guarda una única imagen preservando transparencia (PNG/WebP) y la enlaza a todos los prizeIds enviados. Permitido en borrador o en curso.")
            .DisableAntiforgery()
            .Produces<PrizeImageUploadResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("image/remove", RemovePrizesImage)
            .MapToApiVersion(1)
            .WithName("RemovePrizesImage")
            .WithSummary("Quita la foto de un grupo de premios")
            .WithDescription("Desenlaza la foto de todos los prizeIds indicados. Permitido en borrador o en curso.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
    }

    private static async Task<IResult> CreatePrizes(
        HttpContext httpContext,
        [FromRoute] Guid eventId,
        [FromBody] CreatePrizesRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var dto = new CreatePrizesDto(
            EventId: eventId,
            RoundId: request.RoundId!.Value,
            Name: request.Name!,
            Description: request.Description,
            Value: request.Value,
            SponsorCompanyId: request.SponsorCompanyId,
            SponsorContactId: request.SponsorContactId,
            Quantity: request.Quantity!.Value,
            CreatedById: currentUser.Id,
            SponsorName: request.SponsorName);

        var result = await service.CreatePrizesAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        return Results.Created(
            $"/v{version}/events/{eventId}/raffle",
            result.Value.Select(PrizeResponse.From).ToList());
    }

    private static async Task<IResult> ImportPrizes(
        [FromRoute] Guid eventId,
        [FromBody] ImportPrizesRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var items = request.Prizes!
            .Select(p => new ImportPrizeItemDto(
                RoundNumber: p.RoundNumber!.Value,
                Name: p.Name!,
                Value: p.Value,
                Quantity: p.Quantity!.Value,
                SponsorName: p.SponsorName))
            .ToList();

        var result = await service.ImportPrizesAsync(
            new ImportPrizesDto(eventId, items, currentUser.Id),
            cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem()
            : Results.Ok(new ImportPrizesResponse(result.Value.Created, result.Value.RoundsCreated));
    }

    private static async Task<IResult> UpdatePrize(
        [FromRoute] Guid eventId,
        [FromRoute] Guid prizeId,
        [FromBody] UpdatePrizeRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var dto = new UpdatePrizeDto(
            EventId: eventId,
            PrizeId: prizeId,
            RoundId: request.RoundId!.Value,
            Name: request.Name!,
            Description: request.Description,
            Value: request.Value,
            SponsorCompanyId: request.SponsorCompanyId,
            SponsorContactId: request.SponsorContactId,
            UpdatedById: currentUser.Id,
            SponsorName: request.SponsorName);

        var result = await service.UpdatePrizeAsync(dto, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private static async Task<IResult> UploadPrizeSponsorLogo(
        HttpContext httpContext,
        [FromRoute] Guid eventId,
        [FromRoute] Guid prizeId,
        IFormFile? file,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Se requiere un archivo no vacío en el campo 'file'."]
            });

        // Algunos clientes mandan la ruta completa como nombre; solo interesa el último tramo.
        var fileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(fileName))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["El archivo debe traer un nombre."]
            });

        await using var content = file.OpenReadStream();

        var dto = new SetPrizeSponsorLogoDto(
            EventId: eventId,
            PrizeId: prizeId,
            Content: content,
            FileName: fileName,
            ContentType: string.IsNullOrWhiteSpace(file.ContentType) ? FileSignatures.UndeclaredContentType : file.ContentType,
            UploadedById: currentUser.Id);

        var result = await service.SetPrizeSponsorLogoAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        return Results.Created(
            $"/v{version}/files/{result.Value.Id}",
            PrizeSponsorLogoResponse.From(result.Value));
    }

    private static async Task<IResult> RemovePrizeSponsorLogo(
        [FromRoute] Guid eventId,
        [FromRoute] Guid prizeId,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.RemovePrizeSponsorLogoAsync(eventId, prizeId, currentUser.Id, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private static async Task<IResult> DeletePrize(
        [FromRoute] Guid eventId,
        [FromRoute] Guid prizeId,
        [FromServices] IRaffleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeletePrizeAsync(eventId, prizeId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private const long MaxPrizeImageBytes = 5 * 1024 * 1024;

    private static async Task<IResult> UploadPrizesImage(
        HttpContext httpContext,
        [FromRoute] Guid eventId,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.HasFormContentType)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Se esperaba un formulario multipart/form-data."] });

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Debe adjuntar una imagen."] });

        if (file.Length > MaxPrizeImageBytes)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["La imagen no puede superar los 5 MB."] });

        var prizeIds = new List<Guid>();
        foreach (var raw in form["prizeIds"])
        {
            if (Guid.TryParse(raw, out var id))
                prizeIds.Add(id);
        }

        if (prizeIds.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["prizeIds"] = ["Debe indicar al menos un premio."] });

        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "prize-image.png";

        await using var content = file.OpenReadStream();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? FileSignatures.UndeclaredContentType
            : file.ContentType;

        var result = await service.SetPrizesImageAsync(
            eventId,
            prizeIds,
            content,
            fileName,
            contentType,
            currentUser.Id,
            cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        return Results.Ok(new PrizeImageUploadResponse($"/v{version}/files/{result.Value.Id}/content"));
    }

    private static async Task<IResult> RemovePrizesImage(
        [FromRoute] Guid eventId,
        [FromBody] RemovePrizesImageRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (request.PrizeIds is null || request.PrizeIds.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["prizeIds"] = ["Debe indicar al menos un premio."] });

        var result = await service.RemovePrizesImageAsync(eventId, request.PrizeIds, currentUser.Id, cancellationToken);
        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }
}

public sealed record PrizeImageUploadResponse(string ImageUrl);

public sealed record RemovePrizesImageRequest(IReadOnlyList<Guid> PrizeIds);
