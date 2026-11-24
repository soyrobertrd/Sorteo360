using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>
/// La cartelera pública: lo que un desconocido ve de un evento de MITUR.
/// <para>
/// <b>Es una vista recortada y no un proxy.</b> events-service no se alcanza desde internet y no
/// tiene ninguna excepción que auditar en su política: acá se lee con la identidad propia de este
/// servicio y se vuelve a publicar solo el puñado de campos de la spec <c>01</c> §5. Republicar
/// su respuesta entera filtraría presupuestos.
/// </para>
/// <para>
/// <b>Y no hay caché</b>, a propósito: una lista pública cacheada es una decisión de visibilidad
/// cacheada, que es como un evento privado sigue viéndose cinco minutos después de que alguien
/// lo escondió. events-service caído es un 503 al visitante.
/// </para>
/// </summary>
public sealed class PublicEventEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Ninguna de las dos rutas lleva cuerpo ni filtros: no hay nada que validar que el binder
        // no rechace antes.
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup($"{ApiRoutes.PublicPrefix}/events")
            .WithApiVersionSet(versionSet)
            .WithTags("Public")
            // Tráfico anónimo de internet: es el único sitio del servicio donde esto es correcto,
            // y lo que lo mantiene revisable es que viva bajo el prefijo público y en ningún otro.
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("", List)
            .MapToApiVersion(1)
            .WithName("ListPublicEvents")
            .WithSummary(PublicEventEndpointsDescriptions.ListSummary)
            .WithDescription(PublicEventEndpointsDescriptions.ListDescription)
            .Produces<IReadOnlyList<PublicEventResponse>>(StatusCodes.Status200OK);

        group.MapGet("{id:guid}", Get)
            .MapToApiVersion(1)
            .WithName("GetPublicEvent")
            .WithSummary(PublicEventEndpointsDescriptions.GetSummary)
            .WithDescription(PublicEventEndpointsDescriptions.GetDescription)
            .Produces<PublicEventDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> List(
        [FromServices] IEventDirectory events,
        [FromServices] IRegistrationWindowRepository windows,
        CancellationToken cancellationToken)
    {
        var result = await events.ListPublicEventsAsync(cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        // Una sola lectura para la página entera: la ventana es un left join, y la ausencia de
        // fila es abierta (spec 06 §5.1).
        var shut = await windows.ListShutAsync([.. result.Value.Select(e => e.Id)], cancellationToken);

        return shut.IsFailure
            ? shut.Error.ToProblem()
            : Results.Ok(result.Value
                .Select(e => PublicEventMapper.ToListItem(e, registrationOpen: !shut.Value.Contains(e.Id)))
                .ToArray());
    }

    private static async Task<IResult> Get(
        [FromRoute] Guid id,
        [FromServices] IEventDirectory events,
        [FromServices] IRegistrationWindowRepository windows,
        CancellationToken cancellationToken)
    {
        var result = await events.GetEventAsync(id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        // Un id desconocido, uno privado y uno cancelado salen por la misma puerta y con el mismo
        // código. El cerrado sí sirve: un evento terminado es historia pública, y un enlace
        // compartido la semana pasada tiene que seguir resolviendo.
        if (result.Value.HasNoValue)
            return NotFound();

        var summary = result.Value.Value;

        if (summary is not { IsPublic: true, IsCancelled: false })
            return NotFound();

        // Con la puerta cerrada el detalle sigue sirviendo (spec 06 §5.3): quitarlo dejaría un
        // evento que sigue en pie desaparecido del portal con sus carteles puestos.
        var window = await windows.GetAsync(id, cancellationToken);

        return window.IsFailure
            ? window.Error.ToProblem()
            : Results.Ok(PublicEventMapper.ToDetail(summary, window.Value.IsOpen));
    }

    private static IResult NotFound() => new PublicEventNotFoundError().ToProblem();
}
