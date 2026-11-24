using Sorteo360.Notifications.Core.History;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.History;

/// <summary>
/// Quién ve qué. Una persona con el rol de lectura ve todo; una cuenta de servicio ve solo lo
/// suyo, y quién es "lo suyo" sale del nombre de usuario que Keycloak le da a la cuenta.
/// </summary>
public sealed class HistoryScopeTests
{
    [Theory]
    [InlineData("service-account-events-service", "events")]
    [InlineData("service-account-convocatorias-service", "convocatorias")]
    public void ForCaller_ReturnsTheServiceItIs_WhenTheCallerIsAServiceAccount(string username, string expected)
    {
        var scope = HistoryScope.ForCaller(username);

        scope.IsScoped.ShouldBeTrue();
        scope.SourceService.ShouldBe(expected);
    }

    [Theory]
    [InlineData("maria.perez")]
    [InlineData("central.admin")]
    [InlineData(null)]
    [InlineData("")]
    public void ForCaller_ReturnsEverything_WhenTheCallerIsNotAServiceAccount(string? username)
    {
        var scope = HistoryScope.ForCaller(username);

        scope.IsScoped.ShouldBeFalse();
        scope.SourceService.ShouldBeNull();
    }

    [Fact]
    public void ForCaller_ReturnsTheRawClientNameAsScope_WhenTheServiceAccountClientBreaksTheNaming()
    {
        // Falla cerrado: el nombre se usa tal cual y no coincide con ninguna fila, así que la
        // cuenta ve una lista vacía en vez de verlo todo.
        var scope = HistoryScope.ForCaller("service-account-something-else");

        scope.IsScoped.ShouldBeTrue();
        scope.SourceService.ShouldBe("something-else");
    }

    [Fact]
    public void Everything_ReturnsNoService_WhenRead()
    {
        HistoryScope.Everything.IsScoped.ShouldBeFalse();
        HistoryScope.Everything.SourceService.ShouldBeNull();
    }
}
