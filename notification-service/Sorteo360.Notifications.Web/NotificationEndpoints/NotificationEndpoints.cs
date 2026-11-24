using System.Diagnostics;
using System.Text.Json;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Notifications.Core.Database;
using Sorteo360.Notifications.Core.DTOs;
using Sorteo360.Notifications.Core.DTOs.History;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Web.Auth;
using Sorteo360.Notifications.Web.Filters;
using Sorteo360.Notifications.Web.GlobalEndpoints;
using Sorteo360.Notifications.Web.Validators;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Notifications.Web.NotificationEndpoints;

/// <summary>
/// La puerta de entrada del servicio y su registro: aceptar un envío, y después poder mirarlo.
/// Nada de acá manda correo.
/// <para>
/// El historial es <b>de solo lectura</b> y no por recorte de alcance: sin los valores de
/// <c>data</c> un envío con marcadores no se puede reconstruir, así que un botón de reenvío
/// mandaría un correo distinto del que la pantalla muestra. La consecuencia buena es que esta
/// superficie no tiene ninguna autorización de escritura que diseñar.
/// </para>
/// </summary>
public sealed class NotificationEndpoints : IEndpoint
{
    /// <inheritdoc />
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<SendNotificationRequest>, SendNotificationRequestValidator>();
        services.AddScoped<IValidator<NotificationHistoryRequest>, NotificationHistoryRequestValidator>();
        services.AddScoped<IValidator<OrphanEventsRequest>, OrphanEventsRequestValidator>();
    }

    /// <inheritdoc />
    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/notifications")
            .WithApiVersionSet(versionSet)
            .WithTags("Notifications")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // El rol de envío se le otorga a cuentas de servicio y a ningún compuesto: el endpoint no
        // puede distinguir el token de una persona del de una máquina, y lo que sale por acá sale
        // de la plataforma y no se puede retractar.
        group.MapPost("", Send)
            .MapToApiVersion(1)
            .WithName("SendNotification")
            .WithSummary(NotificationEndpointsDescriptions.SendSummary)
            .WithDescription(NotificationEndpointsDescriptions.SendDescription)
            .RequireAuthorization(AuthorizationPolicies.NotificationsSend)
            .WithPayloadValidation<SendNotificationRequest>()
            .Produces<SendNotificationResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // A partir de acá, solo lectura. Quién lleva el rol de lectura lo decide el realm; lo
        // único que este código distingue es si del otro lado hay una cuenta de servicio, y eso
        // lo resuelve cada handler con el alcance de quien llama.
        group.MapGet("", ListHistory)
            .MapToApiVersion(1)
            .WithName("ListNotificationHistory")
            .WithSummary(NotificationEndpointsDescriptions.ListHistorySummary)
            .WithDescription(NotificationEndpointsDescriptions.ListHistoryDescription)
            .RequireAuthorization(AuthorizationPolicies.NotificationsRead)
            .WithPayloadValidation<NotificationHistoryRequest>()
            .Produces<PagedResponse<NotificationHistoryItemResponse>>();

        // Ruta literal: va antes que nada que pueda parecerse a ella, y no choca con el listado
        // porque el listado no lleva segmento.
        group.MapGet("orphan-events", ListOrphanEvents)
            .MapToApiVersion(1)
            .WithName("ListOrphanNotificationEvents")
            .WithSummary(NotificationEndpointsDescriptions.ListOrphanEventsSummary)
            .WithDescription(NotificationEndpointsDescriptions.ListOrphanEventsDescription)
            .RequireAuthorization(AuthorizationPolicies.NotificationsRead)
            .WithPayloadValidation<OrphanEventsRequest>()
            .Produces<PagedResponse<OrphanEventResponse>>();

        group.MapGet("batches/{batchId:guid}", GetBatch)
            .MapToApiVersion(1)
            .WithName("GetNotificationBatch")
            .WithSummary(NotificationEndpointsDescriptions.GetBatchSummary)
            .WithDescription(NotificationEndpointsDescriptions.GetBatchDescription)
            .RequireAuthorization(AuthorizationPolicies.NotificationsRead)
            .Produces<NotificationBatchResponse>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListHistory(
        HttpContext httpContext,
        [AsParameters] NotificationHistoryRequest request,
        [FromServices] INotificationHistoryReader reader,
        CancellationToken cancellationToken)
    {
        var query = new NotificationHistoryQueryDto(
            Page: request.ResolvedPage,
            PageSize: request.ResolvedPageSize,
            Type: request.Type,
            DeliveryStatus: request.DeliveryStatus,
            RecipientAction: request.RecipientAction,
            Email: request.Email,
            EntityType: request.EntityType,
            EntityId: request.EntityId,
            SourceService: request.SourceService,
            SentFrom: request.SentFrom,
            SentTo: request.SentTo,
            Ids: request.Ids);

        var result = await reader.ListRecipientsAsync(
            query, CallerScope.Of(httpContext.User), cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem()
            : Results.Ok(result.Value.ToResponse(ToResponse));
    }

    private static async Task<IResult> ListOrphanEvents(
        HttpContext httpContext,
        [AsParameters] OrphanEventsRequest request,
        [FromServices] INotificationHistoryReader reader,
        CancellationToken cancellationToken)
    {
        var result = await reader.ListOrphanEventsAsync(
            request.ResolvedPage,
            request.ResolvedPageSize,
            CallerScope.Of(httpContext.User),
            cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem()
            : Results.Ok(result.Value.ToResponse(ToResponse));
    }

    private static async Task<IResult> GetBatch(
        HttpContext httpContext,
        Guid batchId,
        [FromServices] INotificationHistoryReader reader,
        CancellationToken cancellationToken)
    {
        var result = await reader.FindBatchAsync(
            batchId, CallerScope.Of(httpContext.User), cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        // Un envío que el alcance no alcanza se contesta 404 y no 403: un 403 diría que existe, y
        // la existencia de un envío ajeno tampoco es de quien pregunta.
        return result.Value.HasNoValue
            ? Results.NotFound()
            : Results.Ok(ToResponse(result.Value.Value));
    }

    private static async Task<IResult> Send(
        HttpContext httpContext,
        [FromBody] SendNotificationRequest request,
        [FromServices] INotificationService service,
        CancellationToken cancellationToken)
    {
        var dto = new SendNotificationDto(
            Type: request.Type!,
            Language: request.Language!,
            FromAddress: request.FromAddress!,
            FromName: request.FromName,
            Subject: request.Subject!,
            Body: request.Body!,
            SharedData: AsData(request.SharedData),
            Recipients: request.Recipients!
                .Select(recipient => new NotificationRecipientDto(
                    Email: recipient.Email ?? string.Empty,
                    Name: AsName(recipient.Name),
                    Data: AsData(recipient.Data)))
                .ToArray(),
            About: request.About!
                .Select(about => new NotificationAboutDto(
                    SourceService: about.SourceService!,
                    EntityType: about.EntityType!,
                    EntityId: about.EntityId!))
                .ToArray(),
            IdempotencyKey: request.IdempotencyKey!,
            TraceId: TraceIdOf(httpContext));

        var result = await service.AcceptAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        var response = ToResponse(result.Value);

        return Results.Accepted(
            $"/v{version}/notifications/batches/{response.BatchId}",
            response);
    }

    /// <summary>
    /// Normaliza un diccionario ausente a uno vacío y fija la comparación ordinal: las claves vienen
    /// de un JSON, donde dos claves que solo difieren en mayúsculas son dos claves.
    /// </summary>
    private static IReadOnlyDictionary<string, string> AsData(Dictionary<string, string>? data) =>
        data is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(data, StringComparer.Ordinal);

    /// <summary>
    /// Recorta el nombre, y un nombre en blanco cuenta como ausente: así "María " y "María" son el
    /// mismo envío para la huella, y nunca se guarda una cadena vacía.
    /// </summary>
    private static string? AsName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    /// <summary>
    /// La traza de la petición que aceptó el envío, para poder seguirla hasta quien llamó. Se
    /// prefiere la traza distribuida y se cae al identificador de la petición si no hay ninguna.
    /// </summary>
    private static string TraceIdOf(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

    private static NotificationHistoryItemResponse ToResponse(HistoryRecipientDto recipient) => new()
    {
        Id = recipient.Id,
        BatchId = recipient.BatchId,
        Type = recipient.Type,
        Subject = recipient.Subject,
        Language = recipient.Language,
        FromAddress = recipient.FromAddress,
        FromName = recipient.FromName,
        Email = recipient.Email,
        Name = recipient.Name,
        DeliveryStatus = recipient.DeliveryStatus,
        RecipientAction = recipient.RecipientAction,
        DeliveryTrackingAvailable = recipient.DeliveryTrackingAvailable,
        QueuedAt = recipient.QueuedAt,
        SentAt = recipient.SentAt,
        AttemptCount = recipient.AttemptCount,
        LastError = recipient.LastError,
        SgMessageId = recipient.SgMessageId,
        SmtpQueuedAs = recipient.SmtpQueuedAs,
        About = [.. recipient.About.Select(ToResponse)]
    };

    private static NotificationAboutResponse ToResponse(HistoryAboutDto about) => new()
    {
        SourceService = about.SourceService,
        EntityType = about.EntityType,
        EntityId = about.EntityId
    };

    private static NotificationBatchResponse ToResponse(HistoryBatchDto batch) => new()
    {
        Id = batch.Id,
        Type = batch.Type,
        Subject = batch.Subject,
        Body = batch.Body,
        Language = batch.Language,
        FromAddress = batch.FromAddress,
        FromName = batch.FromName,
        DeliveryTrackingAvailable = batch.DeliveryTrackingAvailable,
        BodySha256 = batch.BodySha256,
        CreatedAt = batch.CreatedAt,
        About = [.. batch.About.Select(ToResponse)],
        Recipients = [.. batch.Recipients.Select(ToResponse)]
    };

    /// <summary>
    /// El evento huérfano con su JSON crudo como JSON. La columna es <c>jsonb</c>, así que el texto
    /// siempre parsea; mandarlo como cadena obligaría a quien lo lee a volver a parsearlo.
    /// </summary>
    private static OrphanEventResponse ToResponse(OrphanEventDto orphan) => new()
    {
        Id = orphan.Id,
        SgEventId = orphan.SgEventId,
        EventType = orphan.EventType,
        OccurredAt = orphan.OccurredAt,
        Payload = JsonDocument.Parse(orphan.Payload).RootElement.Clone()
    };

    private static SendNotificationResponse ToResponse(AcceptedNotification accepted) => new()
    {
        BatchId = accepted.BatchId,
        Results = [.. accepted.Results.Select(result => new RecipientResultResponse
        {
            Id = result.Id,
            Recipient = result.Recipient,
            Status = result.Status,
            Reason = result.Reason
        })]
    };
}
