using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class PrizeConfiguration : IEntityTypeConfiguration<PrizeData>
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 1000;

    /// <summary>El valor es texto tal como lo escribió el operador (<c>RD$8,000</c>), no un monto.</summary>
    private const int ValueMaxLength = 100;

    public void Configure(EntityTypeBuilder<PrizeData> builder)
    {
        builder.ToTable("prize", "events", t =>
        {
            t.HasCheckConstraint(
                "ck_prize_sponsor_contact_needs_company",
                "sponsor_contact_id IS NULL OR sponsor_company_id IS NOT NULL");
            t.HasCheckConstraint(
                "ck_prize_sponsor_logo_needs_company",
                "sponsor_logo_file_id IS NULL OR sponsor_company_id IS NOT NULL OR sponsor_name IS NOT NULL");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.RoundId)
            .HasColumnName("round_id")
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(DescriptionMaxLength);

        builder.Property(p => p.Value)
            .HasColumnName("value")
            .HasMaxLength(ValueMaxLength);

        // Ids del CRM y ningún nombre: la empresa y el contacto se nombran al leer (ADR-0013).
        builder.Property(p => p.SponsorCompanyId)
            .HasColumnName("sponsor_company_id");

        builder.Property(p => p.SponsorContactId)
            .HasColumnName("sponsor_contact_id");

        builder.Property(p => p.SponsorName)
            .HasColumnName("sponsor_name")
            .HasMaxLength(NameMaxLength);

        builder.Property(p => p.SponsorLogoFileId)
            .HasColumnName("sponsor_logo_file_id");

        builder.Property(p => p.ImageStoredFileId)
            .HasColumnName("image_stored_file_id");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(p => p.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(p => p.UpdatedById)
            .HasColumnName("updated_by_id")
            .IsRequired();

        // FK hacia RaffleRoundData sin navigation property. Restrict a propósito: una ronda con
        // premios no se elimina, y el servicio responde ROUND_NOT_EMPTY antes de llegar acá. La FK
        // es el respaldo para un premio que entró en la ronda después de que se la viera vacía.
        builder.HasOne<RaffleRoundData>()
            .WithMany()
            .HasForeignKey(p => p.RoundId)
            .HasConstraintName("fk_prize_raffle_round")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.RoundId, "ix_prize_round_id");

        // Restrict: el logo es un enlace y el archivo nunca se borra por él (ADR-0014). Reemplazarlo o
        // quitarlo repunta o vacía la columna; los bytes quedan para la purga de retención.
        builder.HasOne<StoredFileData>()
            .WithMany()
            .HasForeignKey(p => p.SponsorLogoFileId)
            .HasConstraintName("fk_prize_sponsor_logo_stored_file")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.SponsorLogoFileId, "ix_prize_sponsor_logo_file_id");

        builder.HasOne<StoredFileData>()
            .WithMany()
            .HasForeignKey(p => p.ImageStoredFileId)
            .HasConstraintName("fk_prize_image_stored_file")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.ImageStoredFileId, "ix_prize_image_stored_file_id");
    }
}
