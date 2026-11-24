using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Sorteo360.Events.Core.DTOs;
using Sorteo360.Events.Core.Identity;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Platform.Errors;
using Sorteo360.Platform.Filters;
using Sorteo360.Platform.GlobalEndpoints;

namespace Sorteo360.Events.Web.RaffleEndpoints;

/// <summary>
/// Las rondas del sorteo de un evento. Se leen con el sorteo (<c>GET …/raffle</c>); acá solo se
/// escriben, y todas las escrituras exigen <c>raffles:write</c>.
/// </summary>
public sealed class RaffleRoundEndpoints : IEndpoint
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IValidator<CreateRaffleRoundRequest>, Validators.CreateRaffleRoundRequestValidator>();
        services.AddScoped<IValidator<UpdateRaffleRoundRequest>, Validators.UpdateRaffleRoundRequestValidator>();
    }

    public static void DefineEndpoints(IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("v{version:apiVersion}/events/{eventId:guid}/raffle/rounds")
            .WithApiVersionSet(versionSet)
            .WithTags("Raffles")
            .RequireAuthorization(AuthorizationPolicies.RafflesWrite)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("", CreateRound)
            .MapToApiVersion(1)
            .WithName("CreateRaffleRound")
            .WithSummary(RaffleEndpointsDescriptions.CreateRoundSummary)
            .WithDescription(RaffleEndpointsDescriptions.CreateRoundDescription)
            .WithValidation<CreateRaffleRoundRequest>()
            .Produces<Guid>(StatusCodes.Status201Created);

        group.MapPatch("{roundId:guid}", UpdateRound)
            .MapToApiVersion(1)
            .WithName("UpdateRaffleRound")
            .WithSummary(RaffleEndpointsDescriptions.UpdateRoundSummary)
            .WithDescription(RaffleEndpointsDescriptions.UpdateRoundDescription)
            .WithValidation<UpdateRaffleRoundRequest>()
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("{roundId:guid}", DeleteRound)
            .MapToApiVersion(1)
            .WithName("DeleteRaffleRound")
            .WithSummary(RaffleEndpointsDescriptions.DeleteRoundSummary)
            .WithDescription(RaffleEndpointsDescriptions.DeleteRoundDescription)
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> CreateRound(
        HttpContext httpContext,
        [FromRoute] Guid eventId,
        [FromBody] CreateRaffleRoundRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var dto = new CreateRaffleRoundDto(
            EventId: eventId,
            Name: request.Name!,
            Description: request.Description,
            CreatedById: currentUser.Id);

        var result = await service.CreateRoundAsync(dto, cancellationToken);

        if (result.IsFailure)
            return result.Error.ToProblem();

        var version = httpContext.RequestedApiVersion?.ToString() ?? "1";
        return Results.Created($"/v{version}/events/{eventId}/raffle", result.Value);
    }

    private static async Task<IResult> UpdateRound(
        [FromRoute] Guid eventId,
        [FromRoute] Guid roundId,
        [FromBody] UpdateRaffleRoundRequest request,
        [FromServices] IRaffleService service,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var dto = new UpdateRaffleRoundDto(
            EventId: eventId,
            RoundId: roundId,
            Name: request.Name!,
            Description: request.Description,
            UpdatedById: currentUser.Id);

        var result = await service.UpdateRoundAsync(dto, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }

    private static async Task<IResult> DeleteRound(
        [FromRoute] Guid eventId,
        [FromRoute] Guid roundId,
        [FromServices] IRaffleService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteRoundAsync(eventId, roundId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem() : Results.NoContent();
    }
}
