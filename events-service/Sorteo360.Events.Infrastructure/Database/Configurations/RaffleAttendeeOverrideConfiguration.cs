using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class RaffleAttendeeOverrideConfiguration : IEntityTypeConfiguration<RaffleAttendeeOverrideData>
{
    /// <summary>El mismo tope que la descripción de una ronda (spec 01 §4.2).</summary>
    private const int NoteMaxLength = 1000;

    public void Configure(EntityTypeBuilder<RaffleAttendeeOverrideData> builder)
    {
        builder.ToTable("raffle_attendee_override", "events", t =>
        {
            t.HasCheckConstraint("ck_raffle_attendee_override_decision",
                "decision IN ('included', 'excluded')");
        });

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id");

        builder.Property(o => o.RaffleId)
            .HasColumnName("raffle_id")
            .IsRequired();

        builder.Property(o => o.EmailNormalized)
            .HasColumnName("email_normalized")
            .IsRequired();

        builder.Property(o => o.Decision)
            .HasColumnName("decision")
            .IsRequired();

        builder.Property(o => o.Note)
            .HasColumnName("note")
            .HasMaxLength(NoteMaxLength);

        builder.Property(o => o.DecidedById)
            .HasColumnName("decided_by_id")
            .IsRequired();

        builder.Property(o => o.DecidedAt)
            .HasColumnName("decided_at")
            .IsRequired();

        // FK hacia RaffleData sin navigation property en ninguno de los dos lados.
        builder.HasOne<RaffleData>()
            .WithMany()
            .HasForeignKey(o => o.RaffleId)
            .HasConstraintName("fk_raffle_attendee_override_raffle")
            .OnDelete(DeleteBehavior.Restrict);

        // Una decisión por persona y sorteo. Es este índice el que lo garantiza, y sobre él se
        // apoya el upsert del repositorio.
        builder.HasIndex(o => new { o.RaffleId, o.EmailNormalized })
            .HasDatabaseName("ux_raffle_attendee_override_raffle_email")
            .IsUnique();
    }
}
