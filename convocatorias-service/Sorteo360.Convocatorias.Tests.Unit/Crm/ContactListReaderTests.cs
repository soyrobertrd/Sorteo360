using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;
using NSubstitute;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Crm;

/// <summary>
/// La regla de "solo las listas de tu OPT" (PGTI-173). Este servicio lee la lista con su propia
/// identidad, que la ve sea de quien sea, así que si la regla falla acá no la para nadie.
/// </summary>
public sealed class ContactListReaderTests
{
    private const string CallerOptCode = "madrid";

    private static readonly Guid ListId = Guid.NewGuid();
    private static readonly Guid OptOfficeId = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();

    private readonly ICrmContactDirectory _directory = Substitute.For<ICrmContactDirectory>();
    private readonly ContactListReader _reader;

    public ContactListReaderTests()
    {
        _reader = new ContactListReader(_directory);
        _directory.FindContactListAsync(ListId, Arg.Any<CancellationToken>())
            .Returns(Maybe.From(new CrmContactList(ListId, OptOfficeId, [Member])));
    }

    [Fact]
    public async Task ReadOwnMemberIdsAsync_ReturnsTheMembers_WhenTheListExists()
    {
        var result = await _reader.ReadOwnMemberIdsAsync(ListId, CallerOptCode, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe([Member]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task ReadOwnMemberIdsAsync_ReturnsOptNotAssignedWithoutCallingTheCrm_WhenTheCallerHasNoOpt(string? optCode)
    {
        var result = await _reader.ReadOwnMemberIdsAsync(ListId, optCode, CancellationToken.None);

        result.Error.Code.ShouldBe(OptNotAssignedError.ErrorCode);
        await _directory.DidNotReceiveWithAnyArgs().FindContactListAsync(default, default);
    }

    [Fact]
    public async Task ReadOwnMemberIdsAsync_ReturnsContactListNotFound_WhenTheCrmHasNoSuchList()
    {
        var result = await _reader.ReadOwnMemberIdsAsync(Guid.NewGuid(), CallerOptCode, CancellationToken.None);

        result.Error.Code.ShouldBe(ContactListNotFoundError.ErrorCode);
    }
}
