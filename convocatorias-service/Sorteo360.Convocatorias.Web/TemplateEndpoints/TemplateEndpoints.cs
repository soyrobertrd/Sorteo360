using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.TemplateEndpoints;

/// <summary>
/// El catálogo de plantillas de correo. <b>Una sola ruta y de lectura</b>: el conjunto lo siembra
/// una migración de datos y en fase 1 nadie edita una plantilla.
/// <para>
/// Sin esta lectura la columna <c>is_campaign_selectable</c> no tendría a quién servir y el
/// frontend no tendría con qué construir el selector que pidió el cliente. Crear, editar y
/// borrar llegan el día que MITUR gestione sus propias plantillas, y llegan como añadido
/// precisamente porque esta lectura ya existe.
/// </para>
/// </summary>
public sealed class TemplateEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // El único filtro es un booleano opcional: no hay nada que un validador pueda rechazar
        // que el binder no rechace antes.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup($"{ApiRoutes.InternalPrefix}/templates")
            .WithApiVersionSet(versionSet)
            .WithTags("Templates")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("", List)
            .MapToApiVersion(1)
            .WithName("ListTemplates")
            .WithSummary(TemplateEndpointsDescriptions.ListSummary)
            .WithDescription(TemplateEndpointsDescriptions.ListDescription)
            .RequireAuthorization(AuthorizationPolicies.ConvocatoriasRead)
            .Produces<IReadOnlyList<TemplateListItemResponse>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> List(
        [AsParameters] ListTemplatesRequest request,
        [FromServices] ITemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.ListAsync(request.Selectable == true, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem()
            : Results.Ok(result.Value.Select(ToResponse).ToArray());
    }

    private static TemplateListItemResponse ToResponse(TemplateSummary template) => new()
    {
        TemplateKey = template.TemplateKey,
        Language = template.Language,
        Purpose = template.Purpose,
        IsCampaignSelectable = template.IsCampaignSelectable,
        Subject = template.Subject
    };
}
