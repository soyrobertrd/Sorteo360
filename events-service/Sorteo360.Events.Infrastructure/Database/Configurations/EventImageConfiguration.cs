using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class EventImageConfiguration : IEntityTypeConfiguration<EventImageData>
{
    public void Configure(EntityTypeBuilder<EventImageData> builder)
    {
        builder.ToTable("event_image", "events");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(i => i.StoredFileId)
            .HasColumnName("stored_file_id")
            .IsRequired();

        // Se guarda como texto: una fila legible sin diccionario, y agregar un tipo no reordena
        // nada de lo que ya está escrito. El valor por omisión es el que no compite: la portada
        // es única, así que una fila que no diga cuál es no puede nacer portada.
        builder.Property(i => i.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasDefaultValue(EventImageKind.Gallery)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(i => i.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(i => i.UpdatedById)
            .HasColumnName("updated_by_id")
            .IsRequired();

        builder.Property(i => i.DeletedAt)
            .HasColumnName("deleted_at");

        // FK hacia el evento sin navigation property en ninguno de los dos lados.
        builder.HasOne<EventData>()
            .WithMany()
            .HasForeignKey(i => i.EventId)
            .HasConstraintName("fk_event_image_event")
            .OnDelete(DeleteBehavior.Restrict);

        // FK hacia el archivo. Los bytes sobreviven al enlace, así que quitar una imagen nunca
        // arrastra su fila de stored_file.
        builder.HasOne<StoredFileData>()
            .WithMany()
            .HasForeignKey(i => i.StoredFileId)
            .HasConstraintName("fk_event_image_stored_file")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(i => i.DeletedAt == null);

        builder.HasIndex(i => i.EventId)
            .HasDatabaseName("ix_event_image_event_id");

        builder.HasIndex(i => i.StoredFileId)
            .HasDatabaseName("ix_event_image_stored_file_id");

        // Una sola portada viva por evento. La regla vive en la base y no solo en el servicio:
        // dos subidas simultáneas pasarían las dos por la comprobación, y lo que las serializa es
        // el índice. Las bajas no cuentan, que es lo que deja reemplazar la portada sin chocar
        // con las anteriores.
        builder.HasIndex(i => i.EventId, "ux_event_image_cover_per_event")
            .IsUnique()
            .HasDatabaseName("ux_event_image_cover_per_event")
            .HasFilter("kind = 'Cover' AND deleted_at IS NULL");
    }
}
