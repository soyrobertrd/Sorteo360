using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Web.GlobalEndpoints;
using Sorteo360.Events.Web.Validators;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.FileEndpoints;

public sealed class FileEndpoints : IEndpoint
{
    /// <summary>
    /// Los bytes de un archivo no cambian nunca —reemplazarlo crea otro id—, así que el navegador
    /// puede quedárselos. Privado porque van tras autenticación y no deben quedar en una caché
    /// compartida.
    /// </summary>
    private const string ImmutableCacheControl = "private, max-age=31536000, immutable";

    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // El servicio de archivos y el almacenamiento los compone DomainServicesConfiguration;
        // acá solo va el validador de la paginación del listado de ausentes.
        services.AddScoped<IValidator<ListMissingFilesRequest>, ListMissingFilesRequestValidator>();
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/files")
            .WithApiVersionSet(versionSet)
            .WithTags("Files")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Va antes que "{id:guid}" por claridad; la restricción de ruta ya impide que "missing"
        // caiga en la otra, así que el orden es de lectura y no de resolución.
        //
        // Sin política propia: la fallback exige usuario autenticado, igual que el resto de las
        // lecturas del servicio. Un rol acá solo escondería nombres de archivo a quien ya puede
        // leer las cotizaciones que esos archivos documentan.
        group.MapGet("missing", ListMissingFiles)
            .MapToApiVersion(1)
            .WithName("ListMissingFiles")
            .WithSummary(FileEndpointsDescriptions.ListMissingFilesSummary)
            .WithDescription(FileEndpointsDescriptions.ListMissingFilesDescription)
            .WithValidation<ListMissingFilesRequest>()
            .Produces<PagedResponse<MissingFileResponse>>();

        group.MapGet("{id:guid}", GetFileById)
            .MapToApiVersion(1)
            .WithName("GetFileById")
            .WithSummary(FileEndpointsDescriptions.GetFileByIdSummary)
            .WithDescription(FileEndpointsDescriptions.GetFileByIdDescription)
            .Produces<FileResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("{id:guid}/content", GetFileContent)
            .MapToApiVersion(1)
            .WithName("GetFileContent")
            .WithSummary(FileEndpointsDescriptions.GetFileContentSummary)
            .WithDescription(FileEndpointsDescriptions.GetFileContentDescription)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> GetFileById(
        [FromRoute] Guid id,
        [FromServices] IFileService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(MapToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static async Task<IResult> ListMissingFiles(
        [AsParameters] ListMissingFilesRequest request,
        [FromServices] IFileService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListMissingAsync(
            request.ResolvedPage,
            request.ResolvedPageSize,
            cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem()
            : Results.Ok(result.Value.ToResponse(MapToMissingResponse));
    }

    private static async Task<IResult> GetFileContent(
        HttpContext httpContext,
        [FromRoute] Guid id,
        [FromServices] IFileService service,
        CancellationToken cancellationToken)
    {
        var result = await service.OpenContentAsync(id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        if (result.Value.HasNoValue)
            return Results.NotFound();

        var content = result.Value.Value;

        httpContext.Response.Headers.CacheControl = ImmutableCacheControl;
        // El contenido lo eligió quien subió el archivo: el navegador no debe adivinar su tipo
        // ni ejecutarlo en el origen de la aplicación.
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";

        // El resumen del contenido es el ETag natural: mismo archivo, mismo valor, y como los
        // bytes son inmutables nunca hay que invalidarlo.
        return Results.Stream(
            content.Content,
            content.File.ContentType,
            content.File.FileName,
            entityTag: new EntityTagHeaderValue($"\"{content.File.Checksum}\""),
            enableRangeProcessing: false);
    }

    private static MissingFileResponse MapToMissingResponse(StoredFile file)
    {
        return new MissingFileResponse
        {
            Id           = file.Id,
            FileName     = file.FileName,
            ContentType  = file.ContentType,
            SizeBytes    = file.SizeBytes,
            CreatedAt    = file.CreatedAt,
            CreatedById  = file.CreatedById,
            // La consulta filtra por marca presente, así que acá nunca es null.
            MissingSince = file.MissingSince!.Value
        };
    }

    private static FileResponse MapToResponse(StoredFile file)
    {
        return new FileResponse
        {
            Id          = file.Id,
            FileName    = file.FileName,
            ContentType = file.ContentType,
            SizeBytes   = file.SizeBytes,
            CreatedAt   = file.CreatedAt,
            Missing     = file.MissingSince is not null
        };
    }
}
