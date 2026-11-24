using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.Validators;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.CityEndpoints;

public sealed class CityEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<UpdateCityRequest>, UpdateCityRequestValidator>();
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        // Las ciudades son parte de la edición del evento: todo el grupo exige events:write.
        var group = app.MapGroup("v{version:apiVersion}/events/{eventId:guid}/cities")
            .WithApiVersionSet(versionSet)
            .WithTags("Cities")
            .RequireAuthorization(AuthorizationPolicies.EventsWrite)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPatch("{cityId:guid}", UpdateCity)
            .MapToApiVersion(1)
            .WithName("UpdateCity")
            .WithSummary(CityEndpointsDescriptions.UpdateCitySummary)
            .WithDescription(CityEndpointsDescriptions.UpdateCityDescription)
            .WithValidation<UpdateCityRequest>()
            .Produces<CityResponse>();
    }

    private static async Task<IResult> UpdateCity(
        [FromRoute] Guid eventId,
        [FromRoute] Guid cityId,
        [FromBody] UpdateCityRequest request,
        [FromServices] IEventService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var timeZone = CityTimeZone.Create(request.TimeZone);
        if (timeZone.IsFailure)
            return timeZone.Error.ToProblem();

        var dto = new UpdateCityDto(
            EventId: eventId,
            CityId: cityId,
            Name: request.Name!,
            Country: request.Country!,
            TimeZone: timeZone.Value,
            Date: request.Date,
            DisplayOrder: request.DisplayOrder,
            UpdatedById: currentUser.Id);

        var result = await service.UpdateCityAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        return result.Value.HasValue
            ? Results.Ok(CityResponse.From(result.Value.Value))
            : Results.NotFound();
    }
}
