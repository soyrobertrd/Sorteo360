using Microsoft.EntityFrameworkCore;
using Sorteo360.Events.Infrastructure.Database;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Database;

/// <summary>
/// El registro de tablas que enlazan con <c>stored_file</c> tiene que decir la verdad. Si una
/// tabla nueva apunta al archivo y nadie la anota, la purga de retención que viene después la
/// ignoraría y borraría archivos vivos: este test es lo que hace que ese olvido no compile.
/// </summary>
public sealed class StoredFileReferencesTests
{
    [Fact]
    public void LinkTables_ReturnsEveryTableWithAForeignKeyToStoredFile_WhenComparedWithTheModel()
    {
        var referencing = TablesReferencingStoredFile();

        referencing.ShouldBe(StoredFileReferences.LinkTables.Order().ToList());
    }

    /// <summary>
    /// Tablas del modelo de EF con una clave foránea hacia <c>stored_file</c>, ordenadas por
    /// nombre. Sale del modelo y no de una lista escrita a mano, que es lo que le da valor.
    /// </summary>
    private static IReadOnlyList<string> TablesReferencingStoredFile()
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseNpgsql("Host=modelo-sin-conexion;Database=events")
            .Options;

        using var context = new EventsDbContext(options);

        return context.Model.GetEntityTypes()
            .Where(entity => entity.GetForeignKeys()
                .Any(key => key.PrincipalEntityType.ClrType == typeof(StoredFileData)))
            .Select(entity => entity.GetTableName()!)
            .Distinct()
            .Order()
            .ToList();
    }
}
