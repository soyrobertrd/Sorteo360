using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.CancellationNoticeEndpoints;

/// <summary>
/// El aviso de cancelación de un evento (spec <c>06</c> §4.7). Una sola ruta, la que llama el
/// diálogo de cancelar y la que vuelve a correr el cubo de <c>07</c> §6.5.
/// <para>
/// <b><c>campaigns:write</c></b>: escribirle a cada inscrito es la misma autoridad que lanzar una
/// campaña, y es el rol de quien está en la pantalla de convocatorias donde se ve el conteo.
/// </para>
/// </summary>
public sealed class CancellationNoticeEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // La ruta no lleva cuerpo: no hay nada que validar.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        app.MapPost($"{ApiRoutes.InternalPrefix}/events/{{eventId:guid}}/cancellation-notice", NotifyCancellation)
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags("Cancellation notice")
            .RequireAuthorization(AuthorizationPolicies.CampaignsWrite)
            .WithName("SendEventCancellationNotice")
            .WithSummary(CancellationNoticeEndpointsDescriptions.NotifySummary)
            .WithDescription(CancellationNoticeEndpointsDescriptions.NotifyDescription)
            .Produces<CancellationNoticeResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// <b>Sin <c>CancellationToken</c> en la firma, a propósito.</b> En un handler de Minimal API
    /// ese parámetro es <c>HttpContext.RequestAborted</c>, y una pestaña cerrada a mitad del aviso
    /// dejaría a la mitad de los inscritos sin enterarse. Lo que no salió queda sin sello y el cubo
    /// de <c>07</c> §6.5 lo muestra igual, pero no hay por qué llegar ahí.
    /// </summary>
    private static async Task<IResult> NotifyCancellation(
        [FromRoute] Guid eventId,
        [FromServices] ICancellationNoticeService notices)
    {
        var result = await notices.NotifyAsync(eventId, CancellationToken.None);

        return result.IsFailure
            ? result.Error.ToProblem()
            : Results.Accepted(value: new CancellationNoticeResponse
            {
                EventId  = eventId,
                Notified = result.Value.Notified,
                Failed   = result.Value.Failed
            });
    }
}
