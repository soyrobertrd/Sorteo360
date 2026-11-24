using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class PrizeDrawConfiguration : IEntityTypeConfiguration<PrizeDrawData>
{
    /// <summary><c>RD-XXXX-XXXX</c> (spec 03 §2.2).</summary>
    private const int RedemptionCodeLength = 12;

    /// <summary>La nota de una anulación (spec 02 §3).</summary>
    private const int VoidNoteMaxLength = 500;

    public void Configure(EntityTypeBuilder<PrizeDrawData> builder)
    {
        builder.ToTable("prize_draw", "events", t =>
        {
            // Un sorteo que no eligió a nadie no ocurrió y no se escribe (spec 02 §2.1).
            t.HasCheckConstraint("ck_prize_draw_eligible_pool_size", "eligible_pool_size > 0");

            t.HasCheckConstraint("ck_prize_draw_redemption_code", $"redemption_code ~ '{RedemptionCode.Pattern}'");

            // Una anulación es entera o no es: motivo, cuándo y quién van juntos. La nota es optativa.
            t.HasCheckConstraint("ck_prize_draw_void_complete",
                "(void_code IS NULL AND voided_at IS NULL AND voided_by_id IS NULL)" +
                " OR (void_code IS NOT NULL AND voided_at IS NOT NULL AND voided_by_id IS NOT NULL)");

            t.HasCheckConstraint("ck_prize_draw_void_code",
                $"void_code IN ({string.Join(", ", PrizeDrawVoidCode.All.Order().Select(c => $"'{c}'"))})");

            // Un conjunto cerrado, como los avisos de la cotización. Null = todavía no se intentó.
            t.HasCheckConstraint("ck_prize_draw_winner_notification_status",
                "winner_notification_status IS NULL OR winner_notification_status IN " +
                $"({string.Join(", ", QuoteNotificationStatus.All.Order().Select(c => $"'{c}'"))})");
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id");

        builder.Property(d => d.PrizeId)
            .HasColumnName("prize_id")
            .IsRequired();

        builder.Property(d => d.WinnerAttendeeId)
            .HasColumnName("winner_attendee_id")
            .IsRequired();

        builder.Property(d => d.WinnerName)
            .HasColumnName("winner_name")
            .IsRequired();

        builder.Property(d => d.WinnerCompany)
            .HasColumnName("winner_company");

        builder.Property(d => d.WinnerEmail)
            .HasColumnName("winner_email")
            .IsRequired();

        builder.Property(d => d.DrawnAt)
            .HasColumnName("drawn_at")
            .IsRequired();

        builder.Property(d => d.DrawnById)
            .HasColumnName("drawn_by_id")
            .IsRequired();

        builder.Property(d => d.EligiblePoolSize)
            .HasColumnName("eligible_pool_size")
            .IsRequired();

        builder.Property(d => d.OnePrizePerPerson)
            .HasColumnName("one_prize_per_person")
            .IsRequired();

        builder.Property(d => d.OnePrizePerCompany)
            .HasColumnName("one_prize_per_company")
            .IsRequired();

        builder.Property(d => d.RedemptionCode)
            .HasColumnName("redemption_code")
            .HasMaxLength(RedemptionCodeLength)
            .IsRequired();

        builder.Property(d => d.VoidCode)
            .HasColumnName("void_code");

        builder.Property(d => d.VoidNote)
            .HasColumnName("void_note")
            .HasMaxLength(VoidNoteMaxLength);

        builder.Property(d => d.VoidedAt)
            .HasColumnName("voided_at");

        builder.Property(d => d.VoidedById)
            .HasColumnName("voided_by_id");

        builder.Property(d => d.WinnerNotificationId)
            .HasColumnName("winner_notification_id");

        builder.Property(d => d.WinnerNotificationStatus)
            .HasColumnName("winner_notification_status");

        // Restrict y no cascada: eliminar un premio sorteado borraría un sorteo que el acta atestigua.
        builder.HasOne<PrizeData>()
            .WithMany()
            .HasForeignKey(d => d.PrizeId)
            .HasConstraintName("fk_prize_draw_prize")
            .OnDelete(DeleteBehavior.Restrict);

        // El ganador es la fila congelada, estable desde el arranque.
        builder.HasOne<RaffleAttendeeData>()
            .WithMany()
            .HasForeignKey(d => d.WinnerAttendeeId)
            .HasConstraintName("fk_prize_draw_raffle_attendee")
            .OnDelete(DeleteBehavior.Restrict);

        // A lo sumo un sorteo vivo por premio. Es el respaldo del bloqueo del sorteo y lo que hace
        // seguro el reintento (spec 02 §4, §7.1).
        builder.HasIndex(d => d.PrizeId)
            .HasDatabaseName("ux_prize_draw_prize_id_live")
            .IsUnique()
            .HasFilter("voided_at IS NULL");

        // Único en todos los sorteos: el mostrador busca un código sin saber de qué evento viene
        // (spec 03 §2.3).
        builder.HasIndex(d => d.RedemptionCode)
            .HasDatabaseName("ux_prize_draw_redemption_code")
            .IsUnique();
    }
}
