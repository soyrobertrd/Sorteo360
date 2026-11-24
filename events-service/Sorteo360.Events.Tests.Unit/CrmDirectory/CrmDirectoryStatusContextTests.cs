using Sorteo360.Events.Core.CrmDirectory;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.CrmDirectory;

/// <summary>
/// El estado que se informa en la petición se queda con el peor: si algo se sirvió degradado, la
/// respuesta entera lo es. Sin esa regla, una resolución sana posterior taparía una anterior que no
/// lo fue, y la cabecera diría <c>ok</c> sobre datos incompletos.
/// </summary>
public sealed class CrmDirectoryStatusContextTests
{
    [Fact]
    public void Status_ReturnsNull_WhenNothingWasReported()
    {
        // Una petición que no tocó el directorio no lleva cabecera.
        new CrmDirectoryStatusContext().Status.ShouldBeNull();
    }

    [Theory]
    [InlineData(CrmDirectoryStatus.Ok)]
    [InlineData(CrmDirectoryStatus.Stale)]
    [InlineData(CrmDirectoryStatus.Unavailable)]
    public void Report_ReturnsTheReportedValue_WhenReportedOnce(CrmDirectoryStatus status)
    {
        var context = new CrmDirectoryStatusContext();

        context.Report(status);

        context.Status.ShouldBe(status);
    }

    [Theory]
    [InlineData(CrmDirectoryStatus.Ok, CrmDirectoryStatus.Stale, CrmDirectoryStatus.Stale)]
    [InlineData(CrmDirectoryStatus.Stale, CrmDirectoryStatus.Ok, CrmDirectoryStatus.Stale)]
    [InlineData(CrmDirectoryStatus.Ok, CrmDirectoryStatus.Unavailable, CrmDirectoryStatus.Unavailable)]
    [InlineData(CrmDirectoryStatus.Unavailable, CrmDirectoryStatus.Ok, CrmDirectoryStatus.Unavailable)]
    [InlineData(CrmDirectoryStatus.Unavailable, CrmDirectoryStatus.Stale, CrmDirectoryStatus.Unavailable)]
    public void Report_ReturnsTheWorstStatus_WhenReportedTwiceInAnyOrder(
        CrmDirectoryStatus first,
        CrmDirectoryStatus second,
        CrmDirectoryStatus expected)
    {
        var context = new CrmDirectoryStatusContext();

        context.Report(first);
        context.Report(second);

        context.Status.ShouldBe(expected);
    }
}
