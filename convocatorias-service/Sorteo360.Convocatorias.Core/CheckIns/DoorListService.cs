using System.Globalization;
using CSharpFunctionalExtensions;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Core.CheckIns;

/// <inheritdoc />
/// <remarks>
/// <b>Filtra, ordena y pagina en memoria</b>, como la lista de participantes: un evento tiene a lo
/// sumo unos pocos miles de inscripciones, y la búsqueda sin tildes no se escribe en SQL sin
/// <c>unaccent</c>. Los totales salen de la misma lectura.
/// </remarks>
public sealed class DoorListService : IDoorListService
{
    /// <summary>Mayúsculas y tildes aparte, como la lista de participantes: "Álvarez" va con las aes.</summary>
    private const CompareOptions NameOrder = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private readonly IDoorListRepository _repository;

    public DoorListService(IDoorListRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<Result<DoorList, BaseError>> ListAsync(
        Guid eventId,
        string? search,
        bool? checkedIn,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var read = await _repository.ReadAsync(eventId, cancellationToken);

        if (read.IsFailure)
            return read.Error;

        var rows = read.Value;
        var totals = new DoorListTotals(rows.Count, rows.Count(row => row.CheckedInAt is not null));

        var matcher = new DoorListSearch(search);
        IReadOnlyList<DoorListRow> matching =
        [
            .. rows
                .Where(row => checkedIn is not { } wanted || (row.CheckedInAt is not null) == wanted)
                .Where(matcher.Matches)
                .Order(Comparer<DoorListRow>.Create(CompareForList)),
        ];

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, IDoorListService.MaxPageSize);

        return new DoorList(
            totals,
            new PagedResult<DoorListRow>(
                [.. matching.Skip((page - 1) * pageSize).Take(pageSize)],
                matching.Count,
                page,
                pageSize));
    }

    /// <summary>
    /// Apellido, nombre y referencia. La referencia es única, así que el orden es total y la página
    /// dos nunca repite una fila de la uno.
    /// </summary>
    private static int CompareForList(DoorListRow left, DoorListRow right)
    {
        var names = CultureInfo.InvariantCulture.CompareInfo;

        var byLastName = names.Compare(left.LastName, right.LastName, NameOrder);
        if (byLastName != 0)
            return byLastName;

        var byFirstName = names.Compare(left.FirstName, right.FirstName, NameOrder);
        if (byFirstName != 0)
            return byFirstName;

        return string.CompareOrdinal(left.Reference, right.Reference);
    }
}
