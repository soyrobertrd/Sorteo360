using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Convocatorias.Core.DTOs;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Web.RateLimiting;
using Sorteo360.Convocatorias.Web.Routing;
using Sorteo360.Convocatorias.Web.Validators;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Convocatorias.Web.PublicEndpoints;

/// <summary>
/// El portal de registro: abrir un enlace, mandar el formulario, y después mirar la entrada
/// (spec <c>06</c> §1, §2.3, §4.5).
/// <para>
/// <b>Dos <c>POST</c> para inscribirse y dos <c>GET</c> para leer.</b> Resolver e inscribirse
/// llevan el token en el cuerpo; ver la entrada y su QR entran por el <c>qrCode</c> de la ruta,
/// que es un secreto de menor valor y aparece en los registros de acceso a propósito (§2.3).
/// </para>
/// <para>
/// <b>Las dos de <c>POST</c> llevan el token en el cuerpo.</b> El navegador no manda
/// el fragmento al servidor, así que el token del enlace no llega a ningún registro de acceso;
/// ponerlo en una cadena de consulta lo dejaría escrito en el de nginx, el de Traefik y el de la
/// red del cliente (§2.1). Por eso resolver no es un <c>GET</c>.
/// </para>
/// <para>
/// <b>Un endpoint para inscribirse y no dos.</b> El token es opcional: con él se valida y se ata
/// la referencia, sin él se acepta solo en un evento público. Dos rutas duplicarían cada
/// validación y cada límite de peticiones a cambio de una columna anulable (§1.1).
/// </para>
/// <para>
/// <b>Y ningún rechazo dice por qué.</b> Token inventado, gastado o revocado, y evento
/// desconocido, privado, cancelado, cerrado o ya terminado salen todos por el mismo <c>404</c>:
/// separarlos le diría a quien prueba direcciones cuál está invitada de verdad.
/// </para>
/// </summary>
public sealed class PublicRegistrationEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<ResolveInvitationRequest>, ResolveInvitationRequestValidator>();
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup($"{ApiRoutes.PublicPrefix}/registrations")
            .WithApiVersionSet(versionSet)
            .WithTags("Public")
            // Tráfico anónimo de internet, igual que la cartelera: correcto solo acá, bajo el
            // prefijo público, que es lo que lo mantiene revisable de un vistazo.
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("resolve", Resolve)
            .MapToApiVersion(1)
            .WithName("ResolveRegistrationInvitation")
            .WithSummary(PublicRegistrationEndpointsDescriptions.ResolveSummary)
            .WithDescription(PublicRegistrationEndpointsDescriptions.ResolveDescription)
            .WithValidation<ResolveInvitationRequest>()
            // Eje por token: la enumeración que rota de IP y que el eje global no ve (§3).
            .RequireRateLimiting(PublicRateLimiting.PerTokenPolicy)
            .Produces<ResolvedInvitationResponse>(StatusCodes.Status200OK)
            // La puerta cerrada de la ventana de inscripción (spec 06 §5.3).
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapPost("", Register)
            .MapToApiVersion(1)
            .WithName("CreateRegistration")
            .WithSummary(PublicRegistrationEndpointsDescriptions.RegisterSummary)
            .WithDescription(PublicRegistrationEndpointsDescriptions.RegisterDescription)
            .WithValidation<RegisterRequest>()
            // Eje por correo: usar el portal para bombardear a un tercero reenviando su
            // dirección. Es el más estrecho porque cada permiso es un mensaje que le llega a
            // alguien que no lo pidió.
            .RequireRateLimiting(PublicRateLimiting.PerEmailPolicy)
            .Produces<RegistrationResponse>(StatusCodes.Status201Created)
            // El reenvío del mismo formulario no es un conflicto: devuelve la inscripción que ya
            // había, con sus mismos códigos (§4.1).
            .Produces<RegistrationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        // Las dos lecturas de la entrada. Sin política con nombre: el eje por IP del limitador
        // global ya cubre todo el prefijo público, y estas no tienen token ni correo por los que
        // particionar.
        //
        // El `qrCode` lleva una restricción de largo mínimo: son 43 caracteres de base64url, así
        // que jamás es corto. Sin ella este comodín se tragaría `GET .../registrations/resolve`
        // —que es un `POST` y tiene que seguir contestando 405, no 404— y cualquier segmento
        // corto que un día se añada a la ruta.
        group.MapGet("{qrCode:minlength(20)}", Ticket)
            .MapToApiVersion(1)
            .WithName("GetRegistrationTicket")
            .WithSummary(PublicRegistrationEndpointsDescriptions.TicketSummary)
            .WithDescription(PublicRegistrationEndpointsDescriptions.TicketDescription)
            .Produces<RegistrationTicketResponse>(StatusCodes.Status200OK);

        group.MapGet("{qrCode:minlength(20)}/qr", Qr)
            .MapToApiVersion(1)
            .WithName("GetRegistrationQr")
            .WithSummary(PublicRegistrationEndpointsDescriptions.QrSummary)
            .WithDescription(PublicRegistrationEndpointsDescriptions.QrDescription)
            // Se genera al vuelo, sin disco y sin volumen (§4.5).
            .Produces(StatusCodes.Status200OK, contentType: "image/png");
    }

    private static async Task<IResult> Resolve(
        [FromBody] ResolveInvitationRequest request,
        [FromServices] IRegistrationService registrations,
        CancellationToken cancellationToken)
    {
        var result = await registrations.ResolveAsync(request.Token!, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem()
            : Results.Ok(new ResolvedInvitationResponse
            {
                Event = PublicEventMapper.ToDetail(result.Value.Event),
                Prefill = result.Value.Prefill is { } prefill
                    ? new InvitationPrefillResponse
                    {
                        FirstName = prefill.FirstName,
                        LastName  = prefill.LastName,
                        Email     = prefill.Email
                    }
                    : null
            });
    }

    private static async Task<IResult> Register(
        [FromBody] RegisterRequest request,
        [FromServices] IRegistrationService registrations,
        [FromServices] IRegistrationConfirmationSender confirmations,
        [FromServices] ILogger<PublicRegistrationEndpoints> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await registrations.RegisterAsync(
            new RegisterDto(
                request.EventId,
                request.Token,
                request.FirstName!,
                request.LastName!,
                request.Email!,
                request.Phone,
                request.Company,
                request.JobTitle,
                request.Country),
            cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        // El correo de confirmación, sincrónico dentro del POST (spec 06 §4.4). Se manda igual en
        // una inscripción nueva y en un reenvío —que en la práctica significa "perdí el correo"—,
        // así que va antes de decidir entre 201 y 200. **Su fallo no cambia la respuesta**: la
        // inscripción ya está guardada, y si el transporte no contesta la persona tiene el QR en
        // pantalla y la referencia impresa. Por eso se registra y se sigue, y hasta una excepción
        // inesperada se traga: nada de un correo puede tumbar una inscripción que ya existe.
        await SendConfirmationAsync(confirmations, logger, result.Value, cancellationToken);

        var response = new RegistrationResponse
        {
            Reference = result.Value.Registration.Reference,
            QrCode    = result.Value.Registration.QrCode,
            Event     = PublicEventMapper.ToDetail(result.Value.Event)
        };

        // El 201 apunta con `Location` a la página de la entrada, que ya existe. Es una ruta
        // relativa a propósito: la absoluta pediría el esquema y el host, y detrás de Traefik y
        // del nginx del frontend eso es justo lo que el servicio no conoce de sí mismo.
        // El reenvío (200) no la lleva: no creó nada.
        return result.Value.IsNew
            ? Results.Created($"{httpContext.Request.Path}/{response.QrCode}", response)
            : Results.Ok(response);
    }

    /// <summary>
    /// Manda la confirmación y no deja que su fallo salga: la inscripción ya está guardada (spec
    /// <c>06</c> §4.4). Un <see cref="Result"/> fallido queda en el log; una excepción inesperada
    /// también, porque el correo no puede tumbar una inscripción que ya existe.
    /// </summary>
    private static async Task SendConfirmationAsync(
        IRegistrationConfirmationSender confirmations,
        ILogger logger,
        RegistrationOutcome outcome,
        CancellationToken cancellationToken)
    {
        try
        {
            var sent = await confirmations.SendAsync(outcome.Registration, outcome.Event, cancellationToken);

            if (sent.IsFailure)
            {
                logger.LogWarning(
                    "La confirmación de la inscripción {Reference} no salió: {Error}",
                    outcome.Registration.Reference,
                    sent.Error.Message);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "La confirmación de la inscripción {Reference} lanzó y se ignoró: la inscripción está guardada.",
                outcome.Registration.Reference);
        }
    }

    private static async Task<IResult> Ticket(
        [FromRoute] string qrCode,
        [FromServices] IRegistrationService registrations,
        CancellationToken cancellationToken)
    {
        var result = await registrations.GetTicketAsync(qrCode, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        // Un código inventado sale por el mismo 404 que un token inventado: no hay entrada que
        // enseñar.
        if (result.Value.HasNoValue)
            return new PublicRegistrationNotFoundError().ToProblem();

        var ticket = result.Value.Value;

        return Results.Ok(new RegistrationTicketResponse
        {
            Reference = ticket.Reference,
            Name      = ticket.FullName,
            Company   = ticket.Company,
            Event     = PublicEventMapper.ToDetail(ticket.Event)
        });
    }

    private static async Task<IResult> Qr(
        [FromRoute] string qrCode,
        [FromServices] IRegistrationService registrations,
        [FromServices] IQrImageRenderer renderer,
        [FromServices] PortalSettings portal,
        CancellationToken cancellationToken)
    {
        // Solo se pregunta si el código existe: la imagen codifica una URL armada con el propio
        // código y no lee el evento, así que sigue sirviendo aunque events-service esté caído
        // (§4.5).
        var exists = await registrations.QrCodeExistsAsync(qrCode, cancellationToken);

        if (exists.IsFailure)
            return exists.Error.ToProblem();

        if (!exists.Value)
            return new PublicRegistrationNotFoundError().ToProblem();

        var png = renderer.RenderPng(PortalLink.Ticket(portal.BaseUrl, qrCode));

        return Results.File(png, "image/png");
    }
}
