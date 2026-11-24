using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Files;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.EventEndpoints;

/// <summary>
/// La portada de un evento y su galería promocional.
/// <para>
/// Cuelgan del evento, gobernadas por su misma política de escritura, <c>events:write</c>. Van en
/// su propio grupo y no en el de <see cref="EventEndpoints"/> porque ninguna respuesta de acá
/// lleva una referencia al directorio de OPT, así que tampoco lleva su cabecera.
/// </para>
/// </summary>
public sealed class EventImageEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // El cuerpo es multipart y no hay DTO que validar; el servicio de imágenes y el
        // almacenamiento los compone DomainServicesConfiguration.
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/events/{eventId:guid}/images")
            .WithApiVersionSet(versionSet)
            .WithTags("Events")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("", UploadEventImage)
            .MapToApiVersion(1)
            .WithName("UploadEventImage")
            .WithSummary(EventImageEndpointsDescriptions.UploadSummary)
            .WithDescription(EventImageEndpointsDescriptions.UploadDescription)
            // El cuerpo es multipart y la petición viaja con Bearer, no con cookies: el token
            // antifalsificación no aplica y sin esto el filtro rechazaría toda subida.
            .DisableAntiforgery()
            .RequireAuthorization(AuthorizationPolicies.EventsWrite)
            .Produces<EventImageResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("", ListEventImages)
            .MapToApiVersion(1)
            .WithName("ListEventImages")
            .WithSummary(EventImageEndpointsDescriptions.ListSummary)
            .WithDescription(EventImageEndpointsDescriptions.ListDescription)
            .Produces<IReadOnlyList<EventImageResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("{imageId:guid}", RemoveEventImage)
            .MapToApiVersion(1)
            .WithName("RemoveEventImage")
            .WithSummary(EventImageEndpointsDescriptions.RemoveSummary)
            .WithDescription(EventImageEndpointsDescriptions.RemoveDescription)
            .RequireAuthorization(AuthorizationPolicies.EventsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> UploadEventImage(
        HttpContext httpContext,
        [FromRoute] Guid eventId,
        IFormFile? file,
        [FromForm] string? kind,
        [FromServices] IEventImageService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!TryReadKind(kind, out var imageKind, out var kindError))
            return kindError!.ToProblem();

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

        var dto = new AddEventImageDto(
            EventId: eventId,
            Content: content,
            FileName: fileName,
            ContentType: string.IsNullOrWhiteSpace(file.ContentType) ? FileSignatures.UndeclaredContentType : file.ContentType,
            Kind: imageKind ?? EventImageKind.Gallery,
            CreatedById: currentUser.Id);

        var result = await service.AddAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        // No hay tal evento. Lleva código propio y no un 404 vacío: quien sube distingue así "el
        // evento no está" de "la imagen no está", que se responden desde la misma ruta.
        if (result.Value.HasNoValue)
            return EventImageErrors.EventNotFound().ToProblem();

        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        var uploaded = result.Value.Value;

        return Results.Created(
            $"/v{version}/files/{uploaded.File.Id}",
            EventImageResponse.From(uploaded));
    }

    private static async Task<IResult> ListEventImages(
        [FromRoute] Guid eventId,
        [FromQuery] string? kind,
        [FromServices] IEventImageService service,
        CancellationToken cancellationToken)
    {
        if (!TryReadKind(kind, out var imageKind, out var kindError))
            return kindError!.ToProblem();

        var result = await service.ListAsync(eventId, imageKind, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(result.Value.Value.Select(EventImageResponse.From).ToList())
            : EventImageErrors.EventNotFound().ToProblem();
    }

    private static async Task<IResult> RemoveEventImage(
        [FromRoute] Guid eventId,
        [FromRoute] Guid imageId,
        [FromServices] IEventImageService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.RemoveAsync(eventId, imageId, currentUser.Id, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    /// <summary>
    /// Traduce el <c>kind</c> que llegó como texto al enum, o devuelve el error a responder.
    /// Ausente y vacío salen como null: en la subida significa una foto de galería, y en el
    /// listado, todas.
    /// <para>
    /// Se compara contra los nombres del enum y no con <c>Enum.TryParse</c>, que también acepta el
    /// número subyacente: "0" no es un tipo de imagen y no tiene por qué entrar como
    /// <c>Cover</c>. Es la misma regla que sigue la moneda.
    /// </para>
    /// </summary>
    private static bool TryReadKind(string? text, out EventImageKind? kind, out BaseError? error)
    {
        kind = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (!Enum.GetNames<EventImageKind>().Contains(text, StringComparer.Ordinal))
        {
            error = EventImageErrors.KindNotSupported();
            return false;
        }

        kind = Enum.Parse<EventImageKind>(text);
        return true;
    }
}
