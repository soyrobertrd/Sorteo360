using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Identity;
using Sorteo360.Convocatorias.Core.Uploads;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// Las dos rutas de una carga de destinatarios: subir para revisar, y confirmar.
/// <para>
/// <b>Las dos exigen <c>campaigns:write</c> y las dos van detrás de la guarda de borrador</b>, la
/// misma que hace de solo lectura una campaña en <c>sending</c>. Desde que se retiró la
/// aprobación, <c>draft</c> es el único estado editable, así que esa guarda es la regla entera
/// (spec <c>05</c> §1).
/// </para>
/// <para>
/// Van en su propio archivo y no en <see cref="CampaignEndpoints"/> porque son otra cosa: aquel
/// arma la campaña y su lista desde el CRM, y esto lee un archivo que trajo una persona.
/// </para>
/// </summary>
public sealed class RecipientUploadEndpoints : IEndpoint
{
    private const string UploadGroupRoute = $"{ApiRoutes.InternalPrefix}/campaigns/{{id:guid}}/recipient-uploads";

    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Sin validadores: lo que llega es un archivo, y sus reglas son las de
        // SpreadsheetUploadPolicy, que corren sobre los bytes y no sobre un cuerpo JSON.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup(UploadGroupRoute)
            .WithApiVersionSet(versionSet)
            .WithTags("Campaigns")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("", Stage)
            .MapToApiVersion(1)
            .WithName("StageCampaignRecipientUpload")
            .WithSummary(RecipientUploadEndpointsDescriptions.StageSummary)
            .WithDescription(RecipientUploadEndpointsDescriptions.StageDescription)
            // Es multipart y no hay sesión de navegador con cookie detrás, así que el filtro
            // antifalsificación no aplica; sin esto rechazaría toda subida. Igual que en
            // events-service.
            .DisableAntiforgery()
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .Produces<RecipientUploadResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("confirm", Confirm)
            .MapToApiVersion(1)
            .WithName("ConfirmCampaignRecipientUpload")
            .WithSummary(RecipientUploadEndpointsDescriptions.ConfirmSummary)
            .WithDescription(RecipientUploadEndpointsDescriptions.ConfirmDescription)
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .Produces<RecipientUploadResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> Stage(
        [FromRoute] Guid id,
        IFormFile? file,
        [FromServices] IRecipientUploadService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Se requiere un archivo no vacío en el campo 'file'."]
            });
        }

        // Algunos clientes mandan la ruta completa como nombre; solo interesa el último tramo.
        var fileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["El archivo debe traer un nombre."]
            });
        }

        await using var content = file.OpenReadStream();

        var result = await service.StageAsync(
            new StageUploadRequest(
                id,
                content,
                fileName,
                file.ContentType ?? string.Empty,
                currentUser.Id),
            cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(ToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static async Task<IResult> Confirm(
        [FromRoute] Guid id,
        [FromServices] IRecipientUploadService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.ConfirmAsync(id, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(ToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static RecipientUploadResponse ToResponse(PendingUpload pending)
    {
        var response = ToResponse(pending.Batch);

        return response with
        {
            Rows = [.. pending.Rows.Select(row => new RecipientUploadRowResponse
            {
                SourceRowNumber = row.SourceRowNumber,
                FirstName = row.FirstName,
                LastName = row.LastName,
                Email = row.Email,
                Phone = row.Phone,
                Company = row.CompanyRaw,
                Bucket = row.Bucket,
                ReasonCode = row.ReasonCode,
                ContactId = row.ContactId,
                DuplicateRowNumbers = row.DuplicateRowNumbers,
                CollapsedDuplicates = row.CollapsedDuplicates,
            })],
        };
    }

    private static RecipientUploadResponse ToResponse(CampaignUploadBatch batch) => new()
    {
        Id = batch.Id,
        CampaignId = batch.CampaignId,
        OriginalFileName = batch.OriginalFileName,
        DetectedEncoding = batch.DetectedEncoding,
        RowCount = batch.RowCount,
        Counts = new UploadCountsResponse
        {
            Matched = batch.Counts.Matched,
            Unmatched = batch.Counts.Unmatched,
            Suppressed = batch.Counts.Suppressed,
            AlreadyARecipient = batch.Counts.AlreadyARecipient,
            DuplicateInFile = batch.Counts.DuplicateInFile,
            Malformed = batch.Counts.Malformed,
            CollapsedExactDuplicates = batch.Counts.CollapsedExactDuplicates,
        },
        ConfirmableCount = batch.ConfirmableCount,
        UploadedAt = batch.UploadedAt,
        UploadedById = batch.UploadedById,
        ConfirmedAt = batch.ConfirmedAt,
        ConfirmedById = batch.ConfirmedById,
    };
}
