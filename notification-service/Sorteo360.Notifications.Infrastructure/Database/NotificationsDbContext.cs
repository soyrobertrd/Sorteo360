using Microsoft.EntityFrameworkCore;

namespace Sorteo360.Notifications.Infrastructure.Database;

/// <summary>
/// Contexto de base de datos del servicio de notificaciones.
/// Trabaja con clases de persistencia internas (<c>{Entidad}Data</c>); las entidades
/// de dominio se convierten en el repositorio. Las configuraciones de entidad viven
/// en <c>Configurations/</c> y se descubren por reflexión en <see cref="OnModelCreating"/>.
/// </summary>
public sealed class NotificationsDbContext : DbContext
{
    /// <summary>
    /// Esquema propio del servicio. Cada configuración de entidad lo nombra
    /// explícitamente —<c>ToTable("&lt;tabla&gt;", SchemaName)</c>—, igual que en los
    /// servicios hermanos: el esquema y el nombre de la tabla se declaran, nunca se
    /// dejan a la convención de EF.
    /// </summary>
    public const string SchemaName = "notifications";

    /// <summary>Un POST aceptado: el correo tal como lo escribió quien llama.</summary>
    internal DbSet<NotificationBatchData> NotificationBatches => Set<NotificationBatchData>();

    /// <summary>Las entidades de la plataforma de las que trata cada envío.</summary>
    internal DbSet<NotificationAboutData> NotificationAbout => Set<NotificationAboutData>();

    /// <summary>Una fila por persona, con el estado de su correo.</summary>
    internal DbSet<NotificationRecipientData> NotificationRecipients => Set<NotificationRecipientData>();

    /// <summary>Los eventos del webhook del proveedor, incluidos los huérfanos.</summary>
    internal DbSet<NotificationEventData> NotificationEvents => Set<NotificationEventData>();

    /// <summary>Los valores de sustitución que esperan a que el worker mande su envío.</summary>
    internal DbSet<NotificationPayloadData> NotificationPayloads => Set<NotificationPayloadData>();

    /// <summary>Las direcciones que el proveedor reportó muertas u hostiles.</summary>
    internal DbSet<NotificationSuppressionData> NotificationSuppressions => Set<NotificationSuppressionData>();

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
