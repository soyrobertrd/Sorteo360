using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Unsubscribe;
using Sorteo360.Convocatorias.Web.RateLimiting;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Convocatorias.Web.Validators;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>
/// La baja del portal (spec <c>06</c> §6): un <c>POST</c> con el token en el cuerpo que da de
/// baja al contacto y empuja el opt-out al CRM. <b>La puerta es convocatorias, el estado es el
/// CRM.</b>
/// <para>
/// <b>Un <c>POST</c> y no un <c>GET</c>.</b> El clic del enlace solo abre la página de
/// confirmación; darse de baja es el <c>POST</c> del botón. Un escáner corporativo que precarga
/// cada enlace de un correo no debe dar de baja a una lista de invitados en silencio (§2.4).
/// </para>
/// <para>
/// <b>El eje por token y no el de por correo.</b> A diferencia de la inscripción, esto no manda
/// ningún correo, así que no hay con qué bombardear a un tercero; el límite estrecho de ese eje
/// sobra, y el de por token —contra la enumeración que rota de IP— sí aplica (§3).
/// </para>
/// </summary>
public sealed class PublicUnsubscribeEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<UnsubscribeRequest>, UnsubscribeRequestValidator>();
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup($"{ApiRoutes.PublicPrefix}/unsubscribe")
            .WithApiVersionSet(versionSet)
            .WithTags("Public")
            // Tráfico anónimo de internet, correcto solo acá, bajo el prefijo público.
            .AllowAnonymous()
            // No se anuncia 503 a propósito: a diferencia del resto del portal, acá el CRM caído
            // no sale al público —se traga y la fila queda pendiente (§6)—, así que los únicos
            // estados alcanzables son 404 por token desconocido y 500 por un fallo de base.
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("", Unsubscribe)
            .MapToApiVersion(1)
            .WithName("Unsubscribe")
            .WithSummary(PublicUnsubscribeEndpointsDescriptions.UnsubscribeSummary)
            .WithDescription(PublicUnsubscribeEndpointsDescriptions.UnsubscribeDescription)
            .WithValidation<UnsubscribeRequest>()
            // Eje por token: la enumeración que rota de IP. El de por correo no aplica: acá no se
            // manda ningún correo (§3).
            .RequireRateLimiting(PublicRateLimiting.PerTokenPolicy)
            .Produces<UnsubscribeResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();
    }

    private static async Task<IResult> Unsubscribe(
        [FromBody] UnsubscribeRequest request,
        [FromServices] IUnsubscribeService unsubscribes,
        CancellationToken cancellationToken)
    {
        var result = await unsubscribes.UnsubscribeAsync(request.Token!, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem()
            : Results.Ok(new UnsubscribeResponse { MaskedEmail = result.Value });
    }
}
