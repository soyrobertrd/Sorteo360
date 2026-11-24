using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class RaffleRoundConfiguration : IEntityTypeConfiguration<RaffleRoundData>
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 1000;

    public void Configure(EntityTypeBuilder<RaffleRoundData> builder)
    {
        builder.ToTable("raffle_round", "events", t =>
        {
            t.HasCheckConstraint("ck_raffle_round_number", "number >= 1");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        builder.Property(r => r.RaffleId)
            .HasColumnName("raffle_id")
            .IsRequired();

        builder.Property(r => r.Number)
            .HasColumnName("number")
            .IsRequired();

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(DescriptionMaxLength);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(r => r.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(r => r.UpdatedById)
            .HasColumnName("updated_by_id")
            .IsRequired();

        // FK hacia RaffleData sin navigation property en ninguno de los dos lados.
        builder.HasOne<RaffleData>()
            .WithMany()
            .HasForeignKey(r => r.RaffleId)
            .HasConstraintName("fk_raffle_round_raffle")
            .OnDelete(DeleteBehavior.Restrict);

        // El único (raffle_id, number) no se declara acá: tiene que ser DEFERRABLE para que el
        // renumerado al eliminar una ronda no choque consigo mismo a mitad de la sentencia, y EF no
        // sabe declararlo. Lo crea a mano la migración AddRaffleRoundsAndPrizes. Es él, y no el
        // código, el que garantiza que no haya dos rondas con el mismo número.
        builder.HasIndex(r => r.RaffleId, "ix_raffle_round_raffle_id");
    }
}
