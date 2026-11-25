using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class RaffleAttendeeConfiguration : IEntityTypeConfiguration<RaffleAttendeeData>
{
    /// <summary>El mismo tope que la nota de la decisión manual, de la que es copia.</summary>
    private const int ManualNoteMaxLength = 1000;

    /// <summary>Los siete motivos de exclusión (spec 01 §6), los mismos en las dos columnas.</summary>
    private const string ExclusionReasons =
        "'walk_in', 'contact_unresolved', 'suspended', 'do_not_invite', 'manually_excluded', 'not_awards_member', 'dominican_domain'";

    public void Configure(EntityTypeBuilder<RaffleAttendeeData> builder)
    {
        builder.ToTable("raffle_attendee", "events", t =>
        {
            // Calificado es exactamente no tener motivo: el bombo y sus rechazos no se pisan, y la
            // puerta cuadra con calificados más no calificados.
            t.HasCheckConstraint("ck_raffle_attendee_qualified",
                "qualified = (exclusion_reason IS NULL)");

            t.HasCheckConstraint("ck_raffle_attendee_exclusion_reason",
                $"exclusion_reason IN ({ExclusionReasons})");

            t.HasCheckConstraint("ck_raffle_attendee_overruled_reason",
                $"overruled_reason IN ({ExclusionReasons})");

            t.HasCheckConstraint("ck_raffle_attendee_manual_decision",
                "manual_decision IN ('included', 'excluded')");
        });

        // El sorteo de premios ordena el bombo por este id (spec 02 §2), así que es estable.
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.RaffleId)
            .HasColumnName("raffle_id")
            .IsRequired();

        builder.Property(a => a.IdentityKey)
            .HasColumnName("identity_key")
            .IsRequired();

        builder.Property(a => a.ContactId)
            .HasColumnName("contact_id");

        builder.Property(a => a.FirstName)
            .HasColumnName("first_name")
            .IsRequired();

        builder.Property(a => a.LastName)
            .HasColumnName("last_name")
            .IsRequired();

        builder.Property(a => a.Company)
            .HasColumnName("company");

        builder.Property(a => a.CheckedInAt)
            .HasColumnName("checked_in_at")
            .IsRequired();

        builder.Property(a => a.Qualified)
            .HasColumnName("qualified")
            .IsRequired();

        builder.Property(a => a.ExclusionReason)
            .HasColumnName("exclusion_reason");

        builder.Property(a => a.ManualDecision)
            .HasColumnName("manual_decision");

        builder.Property(a => a.ManualDecidedById)
            .HasColumnName("manual_decided_by_id");

        builder.Property(a => a.ManualDecidedAt)
            .HasColumnName("manual_decided_at");

        builder.Property(a => a.ManualNote)
            .HasColumnName("manual_note")
            .HasMaxLength(ManualNoteMaxLength);

        builder.Property(a => a.OverruledReason)
            .HasColumnName("overruled_reason");

        builder.Property(a => a.ReinstatedAt)
            .HasColumnName("reinstated_at");

        builder.Property(a => a.ReinstatedById)
            .HasColumnName("reinstated_by_id");

        // FK hacia RaffleData sin navigation property en ninguno de los dos lados.
        builder.HasOne<RaffleData>()
            .WithMany()
            .HasForeignKey(a => a.RaffleId)
            .HasConstraintName("fk_raffle_attendee_raffle")
            .OnDelete(DeleteBehavior.Cascade);

        // Una fila por persona y sorteo: la identidad es la identity_key, igual que en la puerta.
        builder.HasIndex(a => new { a.RaffleId, a.IdentityKey })
            .HasDatabaseName("ux_raffle_attendee_raffle_identity_key")
            .IsUnique();
    }
}
