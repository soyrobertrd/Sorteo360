using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Database;

/// <summary>
/// La prueba de que la siembra es <c>InsertData</c> y no <c>HasData</c>.
/// <para>
/// <c>HasData</c> vive en el modelo: EF lo lee de ahí y vuelve a afirmar sus valores en cada
/// migración que se genere después, así que revertiría en silencio una plantilla que alguien
/// editara por los endpoints de administración que el cliente ya pidió. <c>InsertData</c> es una
/// sentencia dentro de una migración y el modelo no sabe nada de ella. Que el modelo no lleve
/// datos sembrados es por tanto la prueba exacta: ninguna migración futura puede volver a
/// afirmar lo que el modelo no tiene.
/// </para>
/// </summary>
public sealed class TemplateSeedIsNotModelManagedTests
{
    /// <summary>
    /// Construye el modelo sin tocar la base: EF solo necesita el proveedor para armarlo, no
    /// una conexión.
    /// </summary>
    private static ConvocatoriasDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ConvocatoriasDbContext>()
            .UseNpgsql("Host=localhost;Database=convocatorias;Username=none;Password=none")
            .Options;

        return new ConvocatoriasDbContext(options);
    }

    /// <summary>
    /// El modelo de diseño, que es el que lee <c>dotnet ef migrations add</c>. El modelo de
    /// ejecución está recortado para ir rápido y ni siquiera guarda los datos sembrados, así que
    /// preguntarle a él no probaría nada.
    /// </summary>
    private static IModel DesignTimeModel(ConvocatoriasDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void DesignTimeModel_ReturnsNoSeedData_WhenAskedForTheTemplateTable()
    {
        using var context = BuildContext();

        var entityType = DesignTimeModel(context).FindEntityType(typeof(TemplateData));

        entityType.ShouldNotBeNull();
        entityType.GetSeedData().ShouldBeEmpty();
    }

    [Fact]
    public void DesignTimeModel_ReturnsNoSeedData_WhenAskedForAnyEntity()
    {
        // La regla es del servicio entero y no de esta tabla: la siguiente que se siembre tiene
        // que caer del mismo lado.
        using var context = BuildContext();

        DesignTimeModel(context).GetEntityTypes()
            .SelectMany(entityType => entityType.GetSeedData())
            .ShouldBeEmpty();
    }

    [Fact]
    public void DesignTimeModel_ReturnsNoMigrationOperations_WhenDiffedAgainstTheSnapshot()
    {
        // Lo que pide el spec —correr una segunda migración y comprobar que no vuelve a afirmar
        // las filas sembradas— preguntado sin escribir una migración de mentira: las operaciones
        // de la siguiente migración salen de este differ, así que si está vacío no hay ninguna
        // que emitir, ni de esquema ni de datos. Con HasData la siembra viviría en el modelo y
        // cualquier edición de la copia aparecería acá como una operación de datos que pisaría
        // lo que un usuario hubiera cambiado.
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
