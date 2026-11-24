using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class RaffleConfiguration : IEntityTypeConfiguration<RaffleData>
{
    /// <summary>Tope del nombre de una autoridad, el mismo que el de quien recibe un premio.</summary>
    private const int NameMaxLength = 200;

    /// <summary>Tope del documento de identidad: texto libre y opaco, como el de quien recibe.</summary>
    private const int IdentityDocumentMaxLength = 100;

    /// <summary>Tope de las instrucciones de retiro, texto libre (spec 03 §1).</summary>
    private const int PickupInstructionsMaxLength = 1000;

    /// <summary>Tope del nombre y del cargo de un firmante del certificado, el mismo que su campo en pantalla.</summary>
    private const int CertificateSignerMaxLength = 100;

    public void Configure(EntityTypeBuilder<RaffleData> builder)
    {
        builder.ToTable("raffle", "events", t =>
        {
            t.HasCheckConstraint("ck_raffle_state",
                "state IN ('draft', 'in_progress', 'completed')");

            // Tres estilos válidos: ruleta, lista secuencial y tablero de ganadores (A33).
            t.HasCheckConstraint("ck_raffle_visual_style",
                "visual_style IN ('roulette', 'sequential_names', 'winner_board')");

            t.HasCheckConstraint("ck_raffle_board_identifier",
                "board_identifier IS NULL OR board_identifier IN ('employee_code', 'ticket')");

            t.HasCheckConstraint("ck_raffle_board_batch_size",
                "board_batch_size IS NULL OR (board_batch_size BETWEEN 1 AND 10)");

            t.HasCheckConstraint("ck_raffle_animation_seconds",
                "animation_seconds BETWEEN 5 AND 30");

            // Quién arrancó y cuándo van juntos: un sorteo arrancado por nadie no se puede atestiguar.
            t.HasCheckConstraint("ck_raffle_started",
                "(started_at IS NULL) = (started_by_id IS NULL)");

            // Quién completó y cuándo van juntos, y están exactamente cuando el sorteo está completado.
            t.HasCheckConstraint("ck_raffle_completed",
                "(completed_at IS NULL) = (completed_by_id IS NULL)" +
                " AND (completed_at IS NOT NULL) = (state = 'completed')");

            // Un firmante del certificado es nombre y cargo, o nada; y el segundo nunca está sin el
            // primero, porque el orden es el de las líneas de firma. Cuatro columnas y no una tabla,
            // como las autoridades: el certificado tiene dos líneas, ni una más.
            t.HasCheckConstraint("ck_raffle_certificate_signer_one",
                "(certificate_signer_one_name IS NULL) = (certificate_signer_one_title IS NULL)");

            t.HasCheckConstraint("ck_raffle_certificate_signer_two",
                "(certificate_signer_two_name IS NULL) = (certificate_signer_two_title IS NULL)" +
                " AND (certificate_signer_two_name IS NULL OR certificate_signer_one_name IS NOT NULL)");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        builder.Property(r => r.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(r => r.State)
            .HasColumnName("state")
            .IsRequired();

        builder.Property(r => r.VisualStyle)
            .HasColumnName("visual_style")
            .IsRequired();

        builder.Property(r => r.ShowPrizeOnScreen)
            .HasColumnName("show_prize_on_screen")
            .IsRequired();

        builder.Property(r => r.AnimationSeconds)
            .HasColumnName("animation_seconds")
            .IsRequired();

        builder.Property(r => r.RequiresPresenceConfirmation)
            .HasColumnName("requires_presence_confirmation")
            .IsRequired();

        builder.Property(r => r.RequiresCheckIn)
            .HasColumnName("requires_check_in")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(r => r.OnePrizePerPerson)
            .HasColumnName("one_prize_per_person")
            .IsRequired();

        builder.Property(r => r.OnePrizePerCompany)
            .HasColumnName("one_prize_per_company")
            .IsRequired();

        builder.Property(r => r.BoardTitle)
            .HasColumnName("board_title")
            .HasMaxLength(NameMaxLength);

        builder.Property(r => r.BoardSubtitle)
            .HasColumnName("board_subtitle")
            .HasMaxLength(NameMaxLength);

        builder.Property(r => r.BoardIdentifier)
            .HasColumnName("board_identifier")
            .HasMaxLength(32);

        builder.Property(r => r.BoardBatchSize)
            .HasColumnName("board_batch_size");

        builder.Property(r => r.DisplayNameMode)
            .HasColumnName("display_name_mode")
            .HasMaxLength(30)
            .HasDefaultValue("full_name")
            .IsRequired();

        builder.Property(r => r.DisplayIdentifierMode)
            .HasColumnName("display_identifier_mode")
            .HasMaxLength(30)
            .HasDefaultValue("ticket_number")
            .IsRequired();

        builder.Property(r => r.DisplayDetailMode)
            .HasColumnName("display_detail_mode")
            .HasMaxLength(30)
            .HasDefaultValue("department")
            .IsRequired();

        builder.Property(r => r.NotaryName)
            .HasColumnName("notary_name")
            .HasMaxLength(NameMaxLength);

        builder.Property(r => r.NotaryIdentityDocument)
            .HasColumnName("notary_identity_document")
            .HasMaxLength(IdentityDocumentMaxLength);

        builder.Property(r => r.WitnessOneName)
            .HasColumnName("witness_one_name")
            .HasMaxLength(NameMaxLength);

        builder.Property(r => r.WitnessOneIdentityDocument)
            .HasColumnName("witness_one_identity_document")
            .HasMaxLength(IdentityDocumentMaxLength);

        builder.Property(r => r.WitnessTwoName)
            .HasColumnName("witness_two_name")
            .HasMaxLength(NameMaxLength);

        builder.Property(r => r.WitnessTwoIdentityDocument)
            .HasColumnName("witness_two_identity_document")
            .HasMaxLength(IdentityDocumentMaxLength);

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

        builder.Property(r => r.StartedAt)
            .HasColumnName("started_at");

        builder.Property(r => r.StartedById)
            .HasColumnName("started_by_id");

        // Sin FK todavía: la tabla de las listas de DO Travel Awards llega con su carga (PGTI-115),
        // que agrega la restricción. Hasta entonces la columna siempre es nula.
        builder.Property(r => r.AwardsBatchId)
            .HasColumnName("awards_batch_id");

        builder.Property(r => r.PickupInstructions)
            .HasColumnName("pickup_instructions")
            .HasMaxLength(PickupInstructionsMaxLength);

        builder.Property(r => r.CertificateSignerOneName)
            .HasColumnName("certificate_signer_one_name")
            .HasMaxLength(CertificateSignerMaxLength);

        builder.Property(r => r.CertificateSignerOneTitle)
            .HasColumnName("certificate_signer_one_title")
            .HasMaxLength(CertificateSignerMaxLength);

        builder.Property(r => r.CertificateSignerTwoName)
            .HasColumnName("certificate_signer_two_name")
            .HasMaxLength(CertificateSignerMaxLength);

        builder.Property(r => r.CertificateSignerTwoTitle)
            .HasColumnName("certificate_signer_two_title")
            .HasMaxLength(CertificateSignerMaxLength);

        builder.Property(r => r.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(r => r.CompletedById)
            .HasColumnName("completed_by_id");

        // Token de concurrencia optimista sobre la columna de sistema xmin de PostgreSQL: el PATCH
        // reemplaza el sorteo entero, así que dos ediciones concurrentes no pueden pisarse en
        // silencio; la segunda falla con DbUpdateConcurrencyException (ADR-0009).
        builder.Property<uint>("xmin")
            .IsRowVersion();

        // FK hacia EventData sin navigation property en ninguno de los dos lados.
        builder.HasOne<EventData>()
            .WithMany()
            .HasForeignKey(r => r.EventId)
            .HasConstraintName("fk_raffle_event")
            .OnDelete(DeleteBehavior.Restrict);

        // A lo sumo un sorteo por evento. Es este índice el que lo garantiza, no el código.
        builder.HasIndex(r => r.EventId)
            .HasDatabaseName("ux_raffle_event_id")
            .IsUnique();
    }
}
