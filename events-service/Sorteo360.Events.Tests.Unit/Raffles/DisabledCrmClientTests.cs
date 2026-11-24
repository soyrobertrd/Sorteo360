using Sorteo360.Events.Infrastructure.Crm;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class DisabledCrmClientTests
{
    [Fact]
    public async Task AllMethods_ReturnEmptyOrNullWithoutNetworkCalls()
    {
        var client = new DisabledCrmClient();

        (await client.ListCompaniesAsync(CancellationToken.None)).ShouldBeEmpty();
        (await client.GetCompanyAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
        (await client.GetCompanyContactsAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
        (await client.GetBankAccountAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
        (await client.CheckMailabilityAsync(new CrmMailabilityRequest([Guid.NewGuid()]), CancellationToken.None))
            .ShouldNotBeNull().ShouldBeEmpty();
        (await client.ListContactsByIdAsync([Guid.NewGuid()], CancellationToken.None))!
            .Items.ShouldNotBeNull().ShouldBeEmpty();
    }
}
