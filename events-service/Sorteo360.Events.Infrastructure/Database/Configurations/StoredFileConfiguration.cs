using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFileData>
{
    public void Configure(EntityTypeBuilder<StoredFileData> builder)
    {
        builder.ToTable("stored_file", "events");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id");

        builder.Property(f => f.AccountId)
            .HasColumnName("account_id")
            .HasDefaultValue(Account.DefaultBanreservasAccountId)
            .IsRequired();

        builder.HasIndex(f => f.AccountId)
            .HasDatabaseName("ix_stored_file_account_id");

        builder.Property(f => f.Provider)
            .HasColumnName("provider")
            .IsRequired();

        builder.Property(f => f.StorageKey)
            .HasColumnName("storage_key")
            .IsRequired();

        builder.Property(f => f.FileName)
            .HasColumnName("file_name")
            .IsRequired();

        builder.Property(f => f.ContentType)
            .HasColumnName("content_type")
            .IsRequired();

        builder.Property(f => f.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(f => f.Checksum)
            .HasColumnName("checksum")
            .IsRequired();

        builder.Property(f => f.MissingSince)
            .HasColumnName("missing_since");

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(f => f.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        // La clave es única por proveedor: dos archivos distintos no pueden apuntar a los mismos
        // bytes, y un alta repetida sale como conflicto en vez de duplicar la fila en silencio.
        builder.HasIndex(f => new { f.Provider, f.StorageKey })
            .IsUnique()
            .HasDatabaseName("ux_stored_file_provider_storage_key");

        // Índice parcial: la consulta del operador pregunta solo por las filas selladas, y en una
        // instalación sana esas son casi ninguna. Un índice sobre la tabla entera pesaría lo que
        // pesa la biblioteca para responder por la excepción.
        builder.HasIndex(f => f.MissingSince)
            .HasFilter("missing_since IS NOT NULL")
            .HasDatabaseName("ix_stored_file_missing_since");
    }
}
