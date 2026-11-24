using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.CrmDirectory;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.CompanyDirectory;

/// <summary>
/// Una empresa que no se resolvió sale como <c>null</c>, nunca como texto de relleno: un centinela
/// sería indistinguible de una empresa que de verdad se llamara así.
/// </summary>
public sealed class CompanyLookupTests
{
    private static readonly Guid CompanyId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Snapshot_ReturnsTheSnapshot_WhenTheIdWasResolved()
    {
        var snapshot = new CompanySnapshot(
            CompanyId, "Hotel Jaragua", "República Dominicana", "Santo Domingo", IsActive: true);

        var lookup = new CompanyLookup(
            CrmDirectoryStatus.Ok,
            new Dictionary<Guid, CompanySnapshot> { [CompanyId] = snapshot });

        lookup.Snapshot(CompanyId).ShouldBe(snapshot);
    }

    [Fact]
    public void Snapshot_ReturnsNull_WhenTheIdWasNotResolved()
    {
        var lookup = CompanyLookup.Empty(CrmDirectoryStatus.Unavailable);

        lookup.Snapshot(CompanyId).ShouldBeNull();
        lookup.Status.ShouldBe(CrmDirectoryStatus.Unavailable);
    }
}
