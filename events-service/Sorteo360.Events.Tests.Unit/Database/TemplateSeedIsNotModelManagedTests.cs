using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Events.Infrastructure.Database;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Database;

/// <summary>
/// La prueba de que la siembra de plantillas es <c>InsertData</c> y no <c>HasData</c>.
///
/// <para>
/// <c>HasData</c> vive en el modelo: EF lo lee de ahí y vuelve a afirmar sus valores en cada
/// migración que se genere después, así que revertiría en silencio una plantilla que alguien
/// editara por los endpoints de administración que el cliente ya pidió. <c>InsertData</c> es una
/// sentencia dentro de una migración y el modelo no sabe nada de ella. Que el modelo no lleve datos
/// sembrados es por tanto la prueba exacta: ninguna migración futura puede volver a afirmar lo que
/// el modelo no tiene.
/// </para>
///
/// <para>
/// Es la gemela de la de convocatorias, a propósito: la regla es de plataforma y no de un servicio,
/// y la siguiente tabla sembrada tiene que caer del mismo lado.
/// </para>
/// </summary>
public sealed class TemplateSeedIsNotModelManagedTests
{
    /// <summary>
    /// Construye el modelo sin tocar la base: EF solo necesita el proveedor para armarlo, no una
    /// conexión.
    /// </summary>
    private static EventsDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseNpgsql("Host=localhost;Database=events;Username=none;Password=none")
            .Options;

        return new EventsDbContext(options);
    }

    /// <summary>
    /// El modelo de diseño, que es el que lee <c>dotnet ef migrations add</c>. El de ejecución está
    /// recortado para ir rápido y ni siquiera guarda los datos sembrados, así que preguntarle a él
    /// no probaría nada.
    /// </summary>
    private static IModel DesignTimeModel(EventsDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void DesignTimeModel_ReturnsNoSeedData_WhenBuilt()
    {
        using var context = BuildContext();

        DesignTimeModel(context).GetEntityTypes()
            .SelectMany(entityType => entityType.GetSeedData())
            .ShouldBeEmpty();
    }

    [Fact]
    public void MigrationsModelDiffer_ReturnsNoOperations_WhenComparedWithTheSnapshot()
    {
        // Correr una segunda migración y comprobar que no vuelve a afirmar las filas sembradas,
        // preguntado sin escribir una migración de mentira: las operaciones de la siguiente salen
        // de este differ, así que un differ vacío significa que no hay ninguna que emitir, ni de
        // esquema ni de datos. Con HasData la copia viviría en el modelo y cualquier edición
        // aparecería acá como una operación de datos que pisaría lo que un usuario hubiera cambiado.
        using var context = BuildContext();

        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        snapshot.ShouldNotBeNull();

        var snapshotModel = context.GetService<IModelRuntimeInitializer>()
            .Initialize((IModel)snapshot.Model, designTime: true, validationLogger: null);

        var operations = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshotModel.GetRelationalModel(),
            DesignTimeModel(context).GetRelationalModel());

        operations.ShouldBeEmpty();
    }
}
