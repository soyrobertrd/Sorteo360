using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sorteo360.Platform.GlobalEndpoints;

/// <summary>
/// Contract implemented by every feature endpoint class. Implementations are
/// auto-discovered via assembly scanning (see <see cref="EndpointExtensions"/>).
/// </summary>
public interface IEndpoint
{
    /// <summary>Maps the routes owned by this endpoint group.</summary>
    static abstract void DefineEndpoints(IEndpointRouteBuilder app);

    /// <summary>Registers the services this endpoint group depends on.</summary>
    static abstract void AddServices(IServiceCollection services, IConfiguration configuration);
}
