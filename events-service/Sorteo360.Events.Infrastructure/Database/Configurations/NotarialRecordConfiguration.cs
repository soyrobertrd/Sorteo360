using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class NotarialRecordConfiguration : IEntityTypeConfiguration<NotarialRecordData>
{
    public void Configure(EntityTypeBuilder<NotarialRecordData> builder)
    {
        builder.ToTable("notarial_record", "events", t =>
        {
            // La primera versión es la 1: la que sale al completar (spec 03 §4.5).
            t.HasCheckConstraint("ck_notarial_record_version", "version >= 1");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        builder.Property(r => r.RaffleId)
            .HasColumnName("raffle_id")
            .IsRequired();

        builder.Property(r => r.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(r => r.StoredFileId)
            .HasColumnName("stored_file_id")
            .IsRequired();

        builder.Property(r => r.GeneratedAt)
            .HasColumnName("generated_at")
            .IsRequired();

        builder.Property(r => r.GeneratedById)
            .HasColumnName("generated_by_id")
            .IsRequired();

        // Restrict en las dos: un acta es evidencia, y nada que se borre se la lleva puesta.
        builder.HasOne<RaffleData>()
            .WithMany()
            .HasForeignKey(r => r.RaffleId)
            .HasConstraintName("fk_notarial_record_raffle")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StoredFileData>()
            .WithMany()
            .HasForeignKey(r => r.StoredFileId)
            .HasConstraintName("fk_notarial_record_stored_file")
            .OnDelete(DeleteBehavior.Restrict);

        // Una fila por versión: dos reemisiones simultáneas no pueden escribir la misma.
        builder.HasIndex(r => new { r.RaffleId, r.Version })
            .HasDatabaseName("ux_notarial_record_raffle_id_version")
            .IsUnique();

        builder.HasIndex(r => r.StoredFileId)
            .HasDatabaseName("ix_notarial_record_stored_file_id");
    }
}
