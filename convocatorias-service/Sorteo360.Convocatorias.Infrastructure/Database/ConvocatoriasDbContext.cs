using Microsoft.EntityFrameworkCore;
using Sorteo360.Convocatorias.Core.Registrations;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Contexto de base de datos para el servicio de Convocatorias.
/// Trabaja con clases de persistencia internas (<c>{Entidad}Data</c>); las entidades
/// de dominio se convierten en el repositorio. Las configuraciones de entidad viven
/// en <c>Configurations/</c> y se descubren por reflexión en <see cref="OnModelCreating"/>.
/// </summary>
public sealed class ConvocatoriasDbContext : DbContext
{
    /// <summary>
    /// Esquema propio del servicio. Cada configuración de entidad lo nombra
    /// explícitamente —<c>ToTable("&lt;tabla&gt;", SchemaName)</c>—, igual que en los
    /// servicios hermanos: el esquema y el nombre de la tabla se declaran, nunca se
    /// dejan a la convención de EF.
    /// </summary>
    public const string SchemaName = "convocatorias";

    /// <summary>El catálogo de plantillas de correo, sembrado por una migración de datos.</summary>
    internal DbSet<TemplateData> Templates => Set<TemplateData>();

    /// <summary>Las campañas: un envío por fila, para un evento y en un idioma.</summary>
    internal DbSet<CampaignData> Campaigns => Set<CampaignData>();

    /// <summary>A quién pretende escribirle cada campaña, con la copia de lo que se mandó.</summary>
    internal DbSet<CampaignRecipientData> CampaignRecipients => Set<CampaignRecipientData>();

    /// <summary>Quién quedó fuera al seleccionar, y por qué.</summary>
    internal DbSet<CampaignExclusionData> CampaignExclusions => Set<CampaignExclusionData>();

    /// <summary>
    /// Quién se inscribió: una fila por persona y por evento, nunca por campaña
    /// (spec <c>06</c> §4).
    /// </summary>
    internal DbSet<RegistrationData> Registrations => Set<RegistrationData>();

    /// <summary>
    /// La puerta de inscripción de cada evento que alguien cerró alguna vez. La única tabla del
    /// servicio que cuelga de un evento y no de una campaña (spec <c>06</c> §5.1).
    /// </summary>
    internal DbSet<RegistrationWindowData> RegistrationWindows => Set<RegistrationWindowData>();

    /// <summary>Las subidas de lista: una fila por archivo, y permanente (spec <c>05</c> §2).</summary>
    internal DbSet<CampaignUploadBatchData> CampaignUploadBatches => Set<CampaignUploadBatchData>();

    /// <summary>Las filas revisadas de una subida. Se borran al confirmar.</summary>
    internal DbSet<CampaignUploadRowData> CampaignUploadRows => Set<CampaignUploadRowData>();

    /// <summary>
    /// Los correos que habrían salido, mientras el transporte es <c>stub</c> (spec <c>04</c>
    /// §4.1). Queda vacía donde el transporte es <c>http</c>, y la migración que traiga el cliente
    /// real la borra.
    /// </summary>
    internal DbSet<StubNotificationData> StubNotifications => Set<StubNotificationData>();

    public ConvocatoriasDbContext(DbContextOptions<ConvocatoriasDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConvocatoriasDbContext).Assembly);

        // De dónde sale el número de `reference` (spec 06 §2.3). Se declara acá y no en una
        // configuración de entidad porque una secuencia no cuelga de ninguna tabla: la usa el
        // repositorio con un `nextval` y ninguna columna la tiene por omisión. Que esté en el
        // modelo es lo que la mete en la migración, que es la única autoridad del esquema.
        modelBuilder.HasSequence<long>(RegistrationCodes.SequenceName, SchemaName)
            .StartsAt(1)
            .IncrementsBy(1);
    }
}
