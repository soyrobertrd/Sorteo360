using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Notifications.Infrastructure.Database.Configurations;

internal sealed class NotificationAboutConfiguration : IEntityTypeConfiguration<NotificationAboutData>
{
    public void Configure(EntityTypeBuilder<NotificationAboutData> builder)
    {
        builder.ToTable("notification_about", NotificationsDbContext.SchemaName);

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();

        builder.Property(a => a.SourceService)
            .HasColumnName("source_service")
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasColumnName("entity_type")
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        // FK hacia el envío sin navigation property en ninguno de los dos lados.
        builder.HasOne<NotificationBatchData>()
            .WithMany()
            .HasForeignKey(a => a.BatchId)
            .HasConstraintName("fk_notification_about_batch")
            .OnDelete(DeleteBehavior.Restrict);

        // La pregunta real del cliente es "todo lo que se mandó sobre esta entidad", y este índice
        // es el que la contesta sin recorrer la tabla.
        builder.HasIndex(a => new { a.EntityType, a.EntityId }, "ix_notification_about_entity");

        builder.HasIndex(a => a.BatchId, "ix_notification_about_batch_id");
    }
}
