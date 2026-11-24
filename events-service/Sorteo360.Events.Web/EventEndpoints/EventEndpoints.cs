using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Errors;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Core.Shared;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.Filters;
using Sorteo360.Events.Web.GlobalEndpoints;
using Sorteo360.Events.Web.Validators;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.EventEndpoints;

public sealed class EventEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<CreateEventRequest>, CreateEventRequestValidator>();
        services.AddScoped<IValidator<ListEventsRequest>, ListEventsRequestValidator>();
        services.AddScoped<IValidator<CancelEventRequest>, CancelEventRequestValidator>();
        services.AddScoped<IValidator<UpdateEventRequest>, UpdateEventRequestValidator>();
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/events")
            .WithApiVersionSet(versionSet)
            .WithTags("Events")
            // Toda respuesta que resuelva nombres contra el CRM sale con las cabeceras de estado. Van
            // en el grupo para que un endpoint nuevo no pueda olvidarlas.
            .AddEndpointFilter<CrmDirectoryStatusFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("", CreateEvent)
            .MapToApiVersion(1)
            .WithName("CreateEvent")
            .WithSummary(EventEndpointsDescriptions.CreateEventSummary)
            .WithDescription(EventEndpointsDescriptions.CreateEventDescription)
            .WithValidation<CreateEventRequest>()
            .RequireAuthorization(AuthorizationPolicies.EventsWrite)
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("", ListEvents)
            .MapToApiVersion(1)
            .WithName("ListEvents")
            .WithSummary(EventEndpointsDescriptions.ListEventsSummary)
            .WithDescription(EventEndpointsDescriptions.ListEventsDescription)
            .WithValidation<ListEventsRequest>()
            .Produces<PagedResponse<EventListItemResponse>>();

        group.MapGet("door", ListDoorEvents)
            .MapToApiVersion(1)
            .WithName("ListDoorEvents")
            .WithSummary(EventEndpointsDescriptions.ListDoorEventsSummary)
            .WithDescription(EventEndpointsDescriptions.ListDoorEventsDescription)
            .RequireAuthorization(AuthorizationPolicies.CheckInRead)
            .Produces<IReadOnlyList<DoorEventResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("{id:guid}", GetEventById)
            .MapToApiVersion(1)
            .WithName("GetEventById")
            .WithSummary(EventEndpointsDescriptions.GetEventByIdSummary)
            .WithDescription(EventEndpointsDescriptions.CrmDirectoryStatusDescription)
            .Produces<EventDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("{id:guid}", UpdateEvent)
            .MapToApiVersion(1)
            .WithName("UpdateEvent")
            .WithSummary(EventEndpointsDescriptions.UpdateEventSummary)
            .WithDescription(EventEndpointsDescriptions.UpdateEventDescription)
            .WithValidation<UpdateEventRequest>()
            .RequireAuthorization(AuthorizationPolicies.EventsWrite)
            .Produces<EventDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("{id:guid}/start", StartEvent)
            .MapToApiVersion(1)
            .WithName("StartEvent")
            .WithSummary(EventEndpointsDescriptions.StartEventSummary)
            .WithDescription(EventEndpointsDescriptions.StartEventDescription)
            .RequireAuthorization(AuthorizationPolicies.EventsStart)
            .Produces<EventDetailResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("{id:guid}/cancel", CancelEvent)
            .MapToApiVersion(1)
            .WithName("CancelEvent")
            .WithSummary(EventEndpointsDescriptions.CancelEventSummary)
            .WithDescription(EventEndpointsDescriptions.CancelEventDescription)
            .WithValidation<CancelEventRequest>()
            .RequireAuthorization(AuthorizationPolicies.EventsCancel)
            .Produces<EventDetailResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("{id:guid}/close", CloseEvent)
            .MapToApiVersion(1)
            .WithName("CloseEvent")
            .WithSummary(EventEndpointsDescriptions.CloseEventSummary)
            .WithDescription(EventEndpointsDescriptions.CloseEventDescription)
            .RequireAuthorization(AuthorizationPolicies.EventsClose)
            .Produces<EventDetailResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> CreateEvent(
        HttpContext httpContext,
        [FromBody] CreateEventRequest request,
        [FromServices] IEventService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!TryReadEstimatedBudget(request.EstimatedBudget, out var estimatedBudget, out var budgetError))
            return budgetError!.ToProblem();

        var timeZone = CityTimeZone.CreateOrDefault(request.TimeZone);
        if (timeZone.IsFailure)
            return timeZone.Error.ToProblem();

        var dto = MapToDto(request, estimatedBudget, timeZone.Value, currentUser.Id);
        var result = await service.CreateAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        return Results.Created($"/v{version}/events/{result.Value}", result.Value);
    }

    private static async Task<IResult> GetEventById(
        [FromRoute] Guid id,
        [FromServices] IEventService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(MapToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static async Task<IResult> ListEvents(
        [AsParameters] ListEventsRequest request,
        [FromServices] IEventService service,
        CancellationToken cancellationToken)
    {
        if (!TryReadSort(request, out var sortField, out var sortOrder, out var sortError))
            return sortError!.ToProblem();

        var dto = MapToListDto(request, sortField, sortOrder);
        var result = await service.ListAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(MapToPagedResponse(result.Value));
    }

    private static async Task<IResult> ListDoorEvents(
        [FromServices] IEventService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListDoorAsync(cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return Results.Ok(result.Value.Select(MapToDoorResponse).ToList());
    }

    private static async Task<IResult> UpdateEvent(
        [FromRoute] Guid id,
        [FromBody] UpdateEventRequest request,
        [FromServices] IEventService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // El idioma no se puede omitir: este PATCH reemplaza la representación editable completa,
        // así que ignorar el nulo dejaría el valor guardado y haría a este campo comportarse
        // distinto de todos los demás del mismo endpoint.
        if (request.Language is null)
            return EventErrors.LanguageRequired().ToProblem();

        if (!TryReadEstimatedBudget(request.EstimatedBudget, out var estimatedBudget, out var budgetError))
            return budgetError!.ToProblem();

        var dto = MapToUpdateDto(id, request, estimatedBudget, currentUser.Id);
        var result = await service.UpdateAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(MapToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static async Task<IResult> StartEvent(
        [FromRoute] Guid id,
        [FromServices] IEventService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.StartAsync(id, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(MapToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static async Task<IResult> CancelEvent(
        [FromRoute] Guid id,
        [FromBody] CancelEventRequest request,
        [FromServices] IEventService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.CancelAsync(id, request.Reason!, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(MapToResponse(result.Value.Value))
            : Results.NotFound();
    }

    private static async Task<IResult> CloseEvent(
        [FromRoute] Guid id,
        [FromServices] IEventService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var result = await service.CloseAsync(id, currentUser.Id, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(MapToResponse(result.Value.Value))
            : Results.NotFound();
    }

    /// <summary>
    /// Mapea el request HTTP al DTO de dominio.
    /// </summary>
    private static CreateEventDto MapToDto(
        CreateEventRequest request, Money? estimatedBudget, CityTimeZone cityTimeZone, Guid actorId)
    {
        return new CreateEventDto(
            Type: EventType.All.First(t => t.Equals(request.Type!, StringComparison.OrdinalIgnoreCase)),
            Name: request.Name!,
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            // Sin idioma se crea en español, y sin visibilidad se crea privado: los dos son
            // opcionales en el cuerpo y obligatorios en la columna, y el valor por omisión vive
            // acá, en el borde, no en la base.
            Language: Canonicalize(request.Language, AllowedLanguage.All) ?? AllowedLanguage.Espanol,
            Visibility: Canonicalize(request.Visibility, EventVisibility.All) ?? EventVisibility.Private,
            SeatingLayout: Canonicalize(request.SeatingLayout, SeatingLayout.All),
            ExternalUrl: request.ExternalUrl,
            Description: request.Description,
            InvitedCount: request.InvitedCount,
            EstimatedBudget: estimatedBudget,
            CityName: string.IsNullOrWhiteSpace(request.City) ? "Santo Domingo" : request.City.Trim(),
            CityCountry: string.IsNullOrWhiteSpace(request.Country) ? "República Dominicana" : request.Country.Trim(),
            CityTimeZone: cityTimeZone,
            CreatedById: actorId);
    }

    /// <summary>
    /// Mapea el request de edición al DTO de dominio. Solo cubre los campos mutables:
    /// el type es inmutable y el costo total se materializa al cerrar.
    /// </summary>
    private static UpdateEventDto MapToUpdateDto(Guid id, UpdateEventRequest request, Money? estimatedBudget, Guid actorId)
    {
        return new UpdateEventDto(
            Id: id,
            Name: request.Name!,
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            // El handler ya rechazó el idioma nulo, así que acá siempre hay uno. La visibilidad
            // nula sí llega, y significa "dejala como está": es el único campo que omitir no borra.
            Language: Canonicalize(request.Language, AllowedLanguage.All)!,
            Visibility: Canonicalize(request.Visibility, EventVisibility.All),
            SeatingLayout: Canonicalize(request.SeatingLayout, SeatingLayout.All),
            ExternalUrl: request.ExternalUrl,
            Description: request.Description,
            InvitedCount: request.InvitedCount,
            EstimatedBudget: estimatedBudget,
            UpdatedById: actorId);
    }

    /// <summary>
    /// Mapea el agregado de dominio al DTO de respuesta HTTP.
    /// </summary>
    private static EventDetailResponse MapToResponse(EventWithCities eventWithCities)
    {
        var e = eventWithCities.Event;

        return new EventDetailResponse
        {
            Id          = e.Id,
            Type        = e.Type,
            Name        = e.Name,
            StartDate   = e.StartDate,
            EndDate     = e.EndDate,
            Status       = e.Status,
            Language     = e.Language,
            Visibility   = e.Visibility,
            SeatingLayout = e.SeatingLayout,
            ExternalUrl  = e.ExternalUrl,
            Description  = e.Description,
            InvitedCount = e.InvitedCount,
            EstimatedBudget = e.EstimatedBudget is not null
                            ? new MoneyResponse { Amount = e.EstimatedBudget.Amount, Currency = e.EstimatedBudget.Currency.ToString() }
                            : null,
            TotalCost    = e.TotalCost is not null
                            ? new MoneyResponse { Amount = e.TotalCost.Amount, Currency = e.TotalCost.Currency.ToString() }
                            : null,
            CancellationReason = e.CancellationReason,
            Cities = eventWithCities.Cities
                .Select(CityResponse.From)
                .ToList(),
            Images = eventWithCities.Images.Select(EventImageResponse.From).ToList(),
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }

    /// <summary>
    /// Comprueba el presupuesto del request y lo traduce al value object del dominio. Devuelve
    /// false y el error a responder si no cumple; un presupuesto ausente sí cumple, y sale como
    /// null, porque el campo es opcional.
    /// <para>
    /// Comprobar y traducir van juntos a propósito: la moneda llega como texto y <c>Currency</c>
    /// es un enum cerrado, así que nadie puede quedarse con el <c>Money</c> sin haber pasado por
    /// la comprobación que lo hace posible.
    /// </para>
    /// <para>
    /// Las dos reglas se deciden acá y no en el dominio porque acá todavía existe lo que hay que
    /// rechazar: una moneda inválida deja de ser representable en cuanto se cruza esa frontera.
    /// Van con código propio para que el formulario sepa cuál de los dos campos marcar sin leer
    /// la prosa.
    /// </para>
    /// <para>
    /// Deliberadamente no se engancha <c>MoneyRequestValidator</c> como hacen los demás requests
    /// con dinero: el filtro de validación del chassis responde <c>ValidationProblem</c>, que no
    /// lleva <c>code</c>. El contrato de este endpoint exige un código legible por máquina en
    /// cada rechazo del presupuesto, así que la comprobación se queda acá. Si el filtro llegara
    /// a emitir código propio, esto se reemplaza por el validador y desaparece.
    /// </para>
    /// </summary>
    private static bool TryReadEstimatedBudget(MoneyRequest? request, out Money? budget, out BaseError? error)
    {
        budget = null;
        error = null;

        if (request is null)
            return true;

        if (request.Currency is null || !TryParseName<Currency>(request.Currency, out var currency, ignoreCase: false))
        {
            error = EventErrors.BudgetCurrencyNotSupported();
            return false;
        }

        if (request.Amount <= 0)
        {
            error = EventErrors.BudgetAmountInvalid();
            return false;
        }

        budget = new Money(request.Amount, currency);
        return true;
    }

    private static DoorEventResponse MapToDoorResponse(DoorEvent doorEvent) => new()
    {
        Id        = doorEvent.Event.Id,
        Name      = doorEvent.Event.Name,
        StartDate = doorEvent.Event.StartDate,
        EndDate   = doorEvent.Event.EndDate,
        Cities    = doorEvent.Cities
            .Select(CityResponse.From)
            .ToList()
    };

    /// <summary>
    /// Mapea el request de listado al DTO de dominio: normaliza los filtros de texto vacíos
    /// a null y canonicaliza el type/status al casing almacenado (el match en la base es
    /// exacto), igual que hace el handler de creación.
    /// </summary>
    private static ListEventsDto MapToListDto(
        ListEventsRequest request,
        EventSortField sortField,
        SortOrder sortOrder)
    {
        return new ListEventsDto(
            Type: Canonicalize(request.Type, EventType.All),
            Status: Canonicalize(request.Status, EventStatus.All),
            Active: request.Active,
            Visibility: Canonicalize(request.Visibility, EventVisibility.All),
            StartDateFrom: request.StartDateFrom,
            StartDateTo: request.StartDateTo,
            SearchTerm: string.IsNullOrWhiteSpace(request.SearchTerm) ? null : request.SearchTerm,
            SortField: sortField,
            SortOrder: sortOrder,
            Page: request.ResolvedPage,
            PageSize: request.ResolvedPageSize);
    }

    /// <summary>
    /// Comprueba el orden pedido y lo traduce a los enums del dominio. Devuelve false y el error a
    /// responder si el campo o el sentido no pertenecen a su conjunto cerrado; ausentes sí cumplen
    /// y salen como el orden de siempre, fecha de inicio descendente.
    /// <para>
    /// Va acá y no en el validador por lo mismo que el presupuesto: el filtro de validación del
    /// chassis responde <c>ValidationProblem</c>, que no lleva <c>code</c>, y el contrato de este
    /// listado exige un código legible por máquina en cada rechazo del orden.
    /// </para>
    /// </summary>
    private static bool TryReadSort(
        ListEventsRequest request,
        out EventSortField field,
        out SortOrder order,
        out BaseError? error)
    {
        field = EventSortField.StartDate;
        order = SortOrder.Desc;
        error = null;

        if (!string.IsNullOrWhiteSpace(request.SortField))
        {
            if (!TryParseName(request.SortField, out field))
            {
                error = EventErrors.SortFieldNotSupported();
                return false;
            }

            // Pedir un campo sin sentido ordena ascendente: quien hace clic en una columna espera
            // verla de menor a mayor la primera vez.
            order = SortOrder.Asc;
        }

        if (!string.IsNullOrWhiteSpace(request.SortOrder) && !TryParseName(request.SortOrder, out order))
        {
            error = EventErrors.SortOrderNotSupported();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Traduce el texto al valor del enum que se llama igual. Contra los nombres y no con
    /// <c>Enum.TryParse</c>, que también acepta el número subyacente: "0" no es un campo de orden
    /// ni una moneda, y no tiene por qué entrar como el primer valor.
    /// <para>
    /// El casing lo decide quien llama: el orden llega de un encabezado de tabla y no distingue,
    /// mientras que la moneda es un código ISO y `usd` no es uno.
    /// </para>
    /// </summary>
    private static bool TryParseName<T>(string value, out T parsed, bool ignoreCase = true) where T : struct, Enum
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var name = Enum.GetNames<T>().FirstOrDefault(n => n.Equals(value, comparison));

        parsed = name is null ? default : Enum.Parse<T>(name);
        return name is not null;
    }

    /// <summary>
    /// Devuelve el valor canónico del conjunto permitido que coincide con <paramref name="value"/>
    /// sin distinguir mayúsculas, o null si el valor está vacío. El validator ya garantiza que un
    /// valor presente pertenece al conjunto.
    /// </summary>
    private static string? Canonicalize(string? value, IReadOnlySet<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return allowed.FirstOrDefault(v => v.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Mapea el resultado paginado de dominio a la envoltura HTTP paginada.
    /// </summary>
    private static PagedResponse<EventListItemResponse> MapToPagedResponse(
        PagedResult<EventWithCover> paged)
    {
        return paged.ToResponse(MapToListItem);
    }

    /// <summary>
    /// Mapea un evento de dominio a su proyección liviana de listado.
    /// </summary>
    private static EventListItemResponse MapToListItem(EventWithCover item)
    {
        var e = item.Event;

        return new EventListItemResponse
        {
            Id           = e.Id,
            Type         = e.Type,
            Name         = e.Name,
            Status       = e.Status,
            StartDate    = e.StartDate,
            EndDate      = e.EndDate,
            Language     = e.Language,
            Visibility   = e.Visibility,
            SeatingLayout = e.SeatingLayout,
            ExternalUrl  = e.ExternalUrl,
            InvitedCount = e.InvitedCount,
            TotalCost    = e.TotalCost is not null
                            ? new MoneyResponse { Amount = e.TotalCost.Amount, Currency = e.TotalCost.Currency.ToString() }
                            : null,
            Cover        = item.Cover is not null ? EventImageResponse.From(item.Cover) : null,
            Cities = item.Cities
                .Select(CityResponse.From)
                .ToList(),
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }
}
