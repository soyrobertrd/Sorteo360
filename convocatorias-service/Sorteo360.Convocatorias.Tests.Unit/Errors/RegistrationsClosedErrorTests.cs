using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Errors;

/// <summary>
/// La puerta cerrada tiene código propio en sus dos caras, y ninguno se puede plegar sobre el del
/// evento cancelado (spec <c>06</c> §5.3 y §5.5): el cancelado se esconde tras un <c>404</c> que no
/// dice por qué, y la puerta cerrada sí lo dice porque el evento sigue en la cartelera.
/// </summary>
public sealed class RegistrationsClosedErrorTests
{
    [Fact]
    public void ErrorCode_ReturnsAConflictDistinctFromTheCancelledEventCode_WhenAPublicWriteMeetsAShutWindow()
    {
        var error = new RegistrationsClosedError();

        error.Kind.ShouldBe(ErrorKind.Conflict);
        error.Code.ShouldBe("REGISTRATIONS_CLOSED");
        error.Code.ShouldNotBe(PublicRegistrationNotFoundError.ErrorCode);
        error.Code.ShouldNotBe(CampaignEventCancelledError.ErrorCode);
    }

    [Fact]
    public void ErrorCode_ReturnsAConflictDistinctFromTheCancelledEventCode_WhenALaunchMeetsAShutWindow()
    {
        var eventId = Guid.NewGuid();

        var error = new CampaignRegistrationsClosedError(eventId);

        error.Kind.ShouldBe(ErrorKind.Conflict);
        error.Code.ShouldNotBe(CampaignEventCancelledError.ErrorCode);
        error.ProblemExtensions["eventId"].ShouldBe(eventId);
    }
}
