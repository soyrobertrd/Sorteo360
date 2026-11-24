using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class PrizeHandoverConfiguration : IEntityTypeConfiguration<PrizeHandoverData>
{
    /// <summary>Quién retiró el premio (spec 03 §3.1).</summary>
    private const int ReceiverNameMaxLength = 200;

    /// <summary>Su documento, texto libre y opaco (spec 03 §3.1).</summary>
    private const int ReceiverIdentityDocumentMaxLength = 100;

    /// <summary>Por qué se revirtió (spec 03 §3.3).</summary>
    private const int ReversalReasonMaxLength = 500;

    public void Configure(EntityTypeBuilder<PrizeHandoverData> builder)
    {
        builder.ToTable("prize_handover", "events", t =>
        {
            // Una reversión es entera o no es: cuándo, quién y por qué van juntos.
            t.HasCheckConstraint("ck_prize_handover_reversal_complete",
                "(reversed_at IS NULL AND reversed_by_id IS NULL AND reversal_reason IS NULL)" +
                " OR (reversed_at IS NOT NULL AND reversed_by_id IS NOT NULL AND reversal_reason IS NOT NULL)");
        });

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasColumnName("id");

        builder.Property(h => h.PrizeDrawId)
            .HasColumnName("prize_draw_id")
            .IsRequired();

        builder.Property(h => h.ReceiverName)
            .HasColumnName("receiver_name")
            .HasMaxLength(ReceiverNameMaxLength)
            .IsRequired();

        builder.Property(h => h.ReceiverIdentityDocument)
            .HasColumnName("receiver_identity_document")
            .HasMaxLength(ReceiverIdentityDocumentMaxLength)
            .IsRequired();

        builder.Property(h => h.ReceiverIsWinner)
            .HasColumnName("receiver_is_winner")
            .IsRequired();

        builder.Property(h => h.SignatureStoredFileId)
            .HasColumnName("signature_stored_file_id")
            .IsRequired();

        builder.Property(h => h.RedeemedAt)
            .HasColumnName("redeemed_at")
            .IsRequired();

        builder.Property(h => h.RedeemedById)
            .HasColumnName("redeemed_by_id")
            .IsRequired();

        builder.Property(h => h.ReversedAt)
            .HasColumnName("reversed_at");

        builder.Property(h => h.ReversedById)
            .HasColumnName("reversed_by_id");

        builder.Property(h => h.ReversalReason)
            .HasColumnName("reversal_reason")
            .HasMaxLength(ReversalReasonMaxLength);

        // Restrict: una entrega es evidencia, y nada que se borre se la lleva puesta.
        builder.HasOne<PrizeDrawData>()
            .WithMany()
            .HasForeignKey(h => h.PrizeDrawId)
            .HasConstraintName("fk_prize_handover_prize_draw")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StoredFileData>()
            .WithMany()
            .HasForeignKey(h => h.SignatureStoredFileId)
            .HasConstraintName("fk_prize_handover_stored_file")
            .OnDelete(DeleteBehavior.Restrict);

        // A lo sumo una entrega viva por sorteo. Es el respaldo del bloqueo del sorteo, y lo que ataja a
        // dos mostradores entregando el mismo premio (spec 03 §1, §3.4).
        builder.HasIndex(h => h.PrizeDrawId)
            .HasDatabaseName("ux_prize_handover_prize_draw_id_live")
            .IsUnique()
            .HasFilter("reversed_at IS NULL");

        builder.HasIndex(h => h.SignatureStoredFileId)
            .HasDatabaseName("ix_prize_handover_signature_stored_file_id");
    }
}
