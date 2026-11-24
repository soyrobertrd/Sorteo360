using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Database;

/// <summary>
/// Las migraciones de EF Core son la única autoridad del esquema, y esta suite las corre
/// contra un PostgreSQL real, el mismo DDL que aplica el arranque en cada entorno.
/// </summary>
public sealed class MigrationsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public MigrationsTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task Migrations_ReturnsNoPendingMigration_WhenAnEmptyDatabaseIsMigrated()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        var pending = await context.Database.GetPendingMigrationsAsync();
        var applied = await context.Database.GetAppliedMigrationsAsync();

        pending.ShouldBeEmpty();
        applied.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Las tres tablas de campañas y ninguna cuarta. El esquema se lee de la base y no del
    /// modelo: es el DDL que de verdad corrió.
    /// </summary>
    [Fact]
    public async Task Migrations_ReturnsTheThreeCampaignTables_WhenApplied()
    {
        var tables = await TableNamesAsync();

        tables.ShouldContain("campaign");
        tables.ShouldContain("campaign_recipient");
        tables.ShouldContain("campaign_exclusion");
    }

    /// <summary>
    /// El paso de aprobación se fue entero (ADR-0017), y se comprueba como <b>ausencia</b>
    /// contra el esquema aplicado y no solo en prosa: una tabla que sobreviviera a la decisión
    /// es exactamente la clase de resto que alguien vuelve a cablear seis meses después.
    /// <para>
    /// El gemelo de esta comprobación en el código —que no exista ninguna política
    /// <c>campaigns:approve</c>— vive en <c>Auth/AuthorizationTests</c>.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("campaign_approval")]
    [InlineData("campaign_decision")]
    [InlineData("campaign_launch")]
    public async Task Migrations_ReturnsNoApprovalEraTable_WhenApplied(string retiredTable)
    {
        var tables = await TableNamesAsync();

        tables.ShouldNotContain(retiredTable);
    }

    /// <summary>Las dos tablas de una carga de lista, que el mismo DDL crea (spec <c>05</c> §2).</summary>
    [Theory]
    [InlineData("campaign_upload_batch")]
    [InlineData("campaign_upload_row")]
    public async Task Migrations_ReturnsTheUploadStagingTables_WhenApplied(string table)
    {
        var tables = await TableNamesAsync();

        tables.ShouldContain(table);
    }

    /// <summary>
    /// Una carga sin confirmar por campaña y cuantas confirmadas haga falta. El índice es
    /// <b>parcial</b>, y eso es el dato: uno total prohibiría la segunda carga de una campaña que
    /// ya confirmó la primera, que es exactamente cómo se componen dos listas de verdad.
    /// </summary>
    [Fact]
    public async Task Migrations_ReturnsAPartialUniqueIndexOnPendingUploads_WhenApplied()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        var definitions = await context.Database
            .SqlQuery<string>($"""
                SELECT indexdef AS "Value"
                FROM pg_indexes
                WHERE schemaname = {ConvocatoriasDbContext.SchemaName}
                  AND indexname = 'ux_campaign_upload_batch_pending_per_campaign'
                """)
            .ToListAsync();

        var definition = definitions.ShouldHaveSingleItem();

        definition.ShouldContain("UNIQUE");
        definition.ShouldContain("campaign_id");
        definition.ShouldContain("confirmed_at IS NULL");
    }

    /// <summary>
    /// La ventana de inscripción es la única tabla del servicio que cuelga de un evento y no de
    /// una campaña (spec <c>06</c> §5.1): una fila por evento, y la ausencia significa abierta.
    /// </summary>
    [Fact]
    public async Task AddRegistrationWindow_ReturnsATableKeyedByTheEventId_WhenApplied()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        var keyColumns = await context.Database
            .SqlQuery<string>($"""
                SELECT attribute.attname AS "Value"
                FROM pg_constraint AS constraint_row
                JOIN pg_class AS table_row ON table_row.oid = constraint_row.conrelid
                JOIN pg_namespace AS schema_row ON schema_row.oid = table_row.relnamespace
                JOIN unnest(constraint_row.conkey) AS key(attnum) ON TRUE
                JOIN pg_attribute AS attribute
                  ON attribute.attrelid = table_row.oid AND attribute.attnum = key.attnum
                WHERE schema_row.nspname = {ConvocatoriasDbContext.SchemaName}
                  AND table_row.relname = 'registration_window'
                  AND constraint_row.contype = 'p'
                """)
            .ToListAsync();

        keyColumns.ShouldBe(["event_id"]);
    }

    /// <summary>La tabla del portal de registro (spec <c>06</c> §4).</summary>
    [Fact]
    public async Task Migrations_ReturnsTheRegistrationTable_WhenApplied()
    {
        var tables = await TableNamesAsync();

        tables.ShouldContain("registration");
    }

    /// <summary>
    /// "La misma persona dos veces" es una regla de la base y no del servicio (spec <c>06</c>
    /// §3): dos peticiones simultáneas con la misma dirección pasan las dos por cualquier
    /// <c>SELECT</c> previo, y lo único que las separa es esto.
    /// <para>
    /// Se lee de <c>pg_constraint</c> y no de <c>pg_indexes</c>, que es el dato: un
    /// <c>CREATE UNIQUE INDEX</c> se comporta igual pero no aparece ahí, así que declararla así
    /// dejaría la regla sin poder leerse del catálogo como lo que es.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Migrations_ReturnsAUniqueConstraintOnEventAndEmail_WhenApplied()
    {
        var columns = await UniqueConstraintColumnsAsync("registration", "ux_registration_event_email");

        columns.ShouldBe(["event_id", "email_normalized"], ignoreOrder: true);
    }

    /// <summary>
    /// Los dos códigos de §2.3, cada uno único. Una colisión de <c>qr_code</c> le entregaría la
    /// entrada de alguien a otra persona, y dos <c>reference</c> iguales dejarían sin respuesta
    /// la búsqueda que hace mesa de ayuda con el número que le dictan por teléfono.
    /// </summary>
    [Theory]
    [InlineData("ux_registration_qr_code", "qr_code")]
    [InlineData("ux_registration_reference", "reference")]
    public async Task Migrations_ReturnsAUniqueConstraintPerRegistrationCode_WhenApplied(string constraint, string column)
    {
        var columns = await UniqueConstraintColumnsAsync("registration", constraint);

        columns.ShouldBe([column]);
    }

    /// <summary>
    /// El sello de revisión de <c>07</c> §6.1: quién miró una ficha que no coincide y cuándo. Las dos
    /// anulables, porque una inscripción nace sin revisar.
    /// </summary>
    [Theory]
    [InlineData("details_reviewed_at")]
    [InlineData("details_reviewed_by_id")]
    public async Task AddDetailsReview_ReturnsANullableColumn_WhenItIsOneOfTheReviewPair(string column)
    {
        (await RegistrationColumnNullabilityAsync(column)).ShouldBe(["YES"]);
    }

    /// <summary>
    /// Los dos estados de sincronización con el CRM, y <b>no hay un tercero</b> (spec <c>06</c>
    /// §4.3): con contacto es que está sincronizada, y con sello y sin contacto es que está
    /// pendiente. Que las dos sean anulables es la condición entera — una obligatoria dejaría sin
    /// escribir la inscripción que el CRM no contestó, que es justo lo que §4.3 se niega a hacer.
    /// </summary>
    [Theory]
    [InlineData("crm_contact_id")]
    [InlineData("crm_sync_failed_at")]
    public async Task AddRegistrationCrmSync_ReturnsANullableColumn_WhenItIsOneOfTheSyncPair(string column)
    {
        (await RegistrationColumnNullabilityAsync(column)).ShouldBe(["YES"]);
    }

    /// <summary>
    /// Y ninguna columna de estado junto a ellas. El estado ya sale de la pareja de anulables, y
    /// un <c>status</c> sería un tercer sitio donde apuntar lo mismo y el primero en quedarse
    /// viejo (spec <c>06</c> §4).
    /// </summary>
    [Theory]
    [InlineData("status")]
    [InlineData("crm_sync_status")]
    public async Task AddRegistrationCrmSync_ReturnsNoStatusColumn_WhenTheSyncStateComesFromTheNullablePair(string column)
    {
        var columns = await RegistrationColumnsAsync();

        columns.ShouldNotContain(column);
    }

    /// <summary>
    /// El check-in de spec <c>09</c> §2: dos columnas anulables sobre <c>registration</c> y ninguna
    /// tabla. Anulables porque toda inscripción ya escrita llegó sin entrar, y la migración tiene que
    /// aplicarse sobre una base que ya tiene filas.
    /// </summary>
    [Theory]
    [InlineData("checked_in_at")]
    [InlineData("checked_in_by_id")]
    public async Task AddCheckIn_ReturnsANullableColumn_WhenItIsOneOfTheCheckInPair(string column)
    {
        (await RegistrationColumnNullabilityAsync(column)).ShouldBe(["YES"]);
    }

    /// <summary>Una hora sin quien la puso, o al revés, no la acepta la base.</summary>
    [Fact]
    public async Task AddCheckIn_Throws_WhenOnlyHalfOfTheCheckInPairIsWritten()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        var unique = Guid.NewGuid().ToString("N");

        var write = () => context.Database.ExecuteSqlAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, first_name, last_name, email, email_normalized, reference, qr_code,
                 contact_details_differ, checked_in_at, created_at, updated_at)
            VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, 'Ana', 'Gómez', {unique + "@acme.com"},
                    {unique + "@acme.com"}, {"MITUR-2026-" + unique}, {unique}, false, now(), now(), now())
            """);

        var error = await Should.ThrowAsync<Npgsql.PostgresException>(write);
        error.ConstraintName.ShouldBe("ck_registration_check_in_pair");
    }

    /// <summary>
    /// Las dos lecturas de quién vino, por evento y por contacto, sobre índices parciales: los que
    /// entraron son la minoría de las filas.
    /// </summary>
    [Theory]
    [InlineData("ix_registration_event_checked_in", "event_id")]
    [InlineData("ix_registration_contact_checked_in", "crm_contact_id")]
    public async Task AddCheckIn_ReturnsAPartialIndexOverCheckedInRows_WhenApplied(string index, string column)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        var definitions = await context.Database
            .SqlQuery<string>($"""
                SELECT indexdef AS "Value"
                FROM pg_indexes
                WHERE schemaname = {ConvocatoriasDbContext.SchemaName}
                  AND indexname = {index}
                """)
            .ToListAsync();

        var definition = definitions.ShouldHaveSingleItem();
        definition.ShouldContain($"({column})");
        definition.ShouldContain("checked_in_at IS NOT NULL");
    }

    /// <summary>El <c>is_nullable</c> de una columna de <c>registration</c>; vacío si no existe.</summary>
    private async Task<IReadOnlyList<string>> RegistrationColumnNullabilityAsync(string column)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<string>($"""
                SELECT is_nullable AS "Value"
                FROM information_schema.columns
                WHERE table_schema = {ConvocatoriasDbContext.SchemaName}
                  AND table_name = 'registration'
                  AND column_name = {column}
                """)
            .ToListAsync();
    }

    /// <summary>Las columnas de <c>registration</c>, leídas del esquema aplicado.</summary>
    private async Task<IReadOnlyList<string>> RegistrationColumnsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<string>($"""
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = {ConvocatoriasDbContext.SchemaName}
                  AND table_name = 'registration'
                """)
            .ToListAsync();
    }

    /// <summary>
    /// De dónde sale el número de <c>reference</c>. Una sola secuencia y no una por año: nunca
    /// entrega el mismo número dos veces por concurrida que esté, y reiniciarla cada enero
    /// pediría o DDL en caliente o una fila contador que pusiera en fila india a todas las
    /// inscripciones del portal.
    /// </summary>
    [Fact]
    public async Task Migrations_ReturnsTheReferenceSequence_WhenApplied()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        var sequences = await context.Database
            .SqlQuery<string>($"""
                SELECT sequence_name AS "Value"
                FROM information_schema.sequences
                WHERE sequence_schema = {ConvocatoriasDbContext.SchemaName}
                """)
            .ToListAsync();

        sequences.ShouldContain(RegistrationCodes.SequenceName);
    }

    /// <summary>Las columnas de una restricción de unicidad, leídas del catálogo.</summary>
    private async Task<IReadOnlyList<string>> UniqueConstraintColumnsAsync(string table, string constraint)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<string>($"""
                SELECT attribute.attname AS "Value"
                FROM pg_constraint AS constraint_row
                JOIN pg_class AS table_row ON table_row.oid = constraint_row.conrelid
                JOIN pg_namespace AS schema_row ON schema_row.oid = table_row.relnamespace
                JOIN unnest(constraint_row.conkey) AS key(attnum) ON TRUE
                JOIN pg_attribute AS attribute
                  ON attribute.attrelid = table_row.oid AND attribute.attnum = key.attnum
                WHERE schema_row.nspname = {ConvocatoriasDbContext.SchemaName}
                  AND table_row.relname = {table}
                  AND constraint_row.conname = {constraint}
                  AND constraint_row.contype = 'u'
                """)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<string>> TableNamesAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();

        return await context.Database
            .SqlQuery<string>($"""
                SELECT table_name AS "Value"
                FROM information_schema.tables
                WHERE table_schema = {ConvocatoriasDbContext.SchemaName}
                """)
            .ToListAsync();
    }
}
