using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.Crm;

/// <inheritdoc cref="IContactListReader" />
public sealed class ContactListReader : IContactListReader
{
    private readonly ICrmContactDirectory _directory;

    public ContactListReader(ICrmContactDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _directory = directory;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<Guid>, BaseError>> ReadOwnMemberIdsAsync(
        Guid contactListId,
        string? callerOptCode,
        CancellationToken cancellationToken)
    {
        // Falla cerrado: sin OPT no hay lista que sea suya, y no hace falta preguntarle al CRM.
        if (string.IsNullOrWhiteSpace(callerOptCode))
            return Result.Failure<IReadOnlyList<Guid>, BaseError>(new OptNotAssignedError());

        var list = await _directory.FindContactListAsync(contactListId, cancellationToken);

        if (list.IsFailure)
            return Result.Failure<IReadOnlyList<Guid>, BaseError>(list.Error);

        if (list.Value.HasNoValue)
            return Result.Failure<IReadOnlyList<Guid>, BaseError>(new ContactListNotFoundError());

        var contactList = list.Value.Value;

        return Result.Success<IReadOnlyList<Guid>, BaseError>(contactList.ContactIds);
    }
}
