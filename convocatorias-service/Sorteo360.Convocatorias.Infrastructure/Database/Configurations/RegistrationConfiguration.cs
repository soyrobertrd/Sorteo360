using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Convocatorias.Infrastructure.Database.Configurations;

internal sealed class RegistrationConfiguration : IEntityTypeConfiguration<RegistrationData>
{
    public void Configure(EntityTypeBuilder<RegistrationData> builder)
    {
        // El check-in es una pareja (spec 09 §2): cuándo y quién, las dos o ninguna. Una hora sin
        // quien la puso no se puede auditar, y un quien sin hora no dice que la persona entró.
        builder.ToTable("registration", ConvocatoriasDbContext.SchemaName, t =>
            t.HasCheckConstraint(
                "ck_registration_check_in_pair",
                "(checked_in_at IS NULL) = (checked_in_by_id IS NULL)"));

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(r => r.CampaignRecipientId)
            .HasColumnName("campaign_recipient_id");

        builder.Property(r => r.FirstName)
            .HasColumnName("first_name")
            .IsRequired();

        builder.Property(r => r.LastName)
            .HasColumnName("last_name")
            .IsRequired();

        builder.Property(r => r.Email)
            .HasColumnName("email")
            .IsRequired();

        builder.Property(r => r.EmailNormalized)
            .HasColumnName("email_normalized")
            .IsRequired();

        builder.Property(r => r.Phone)
            .HasColumnName("phone");

        builder.Property(r => r.Company)
            .HasColumnName("company");

        builder.Property(r => r.JobTitle)
            .HasColumnName("job_title");

        builder.Property(r => r.Country)
            .HasColumnName("country");

        builder.Property(r => r.Reference)
            .HasColumnName("reference")
            .IsRequired();

        builder.Property(r => r.QrCode)
            .HasColumnName("qr_code")
            .IsRequired();

        builder.Property(r => r.ContactDetailsDiffer)
            .HasColumnName("contact_details_differ")
            .IsRequired();

        // Las dos de §4.3, las dos anulables, y ninguna columna de estado al lado: puesto el
        // contacto es "sincronizada", y puesto el sello sin contacto es "pendiente". Un enum
        // sería un tercer sitio donde apuntar lo mismo y el primero en quedarse viejo.
        //
        // Y no apuntan a ninguna clave foránea: el contacto vive en la base del CRM, que es otro
        // servicio y otra base. Lo que se guarda es el id y nada más (CLAUDE.md raíz).
        builder.Property(r => r.CrmContactId)
            .HasColumnName("crm_contact_id");

        builder.Property(r => r.CrmSyncFailedAt)
            .HasColumnName("crm_sync_failed_at");

        builder.Property(r => r.CancellationNotifiedAt)
            .HasColumnName("cancellation_notified_at");

        builder.Property(r => r.DetailsReviewedAt)
            .HasColumnName("details_reviewed_at");

        builder.Property(r => r.DetailsReviewedById)
            .HasColumnName("details_reviewed_by_id");

        builder.Property(r => r.CheckedInAt)
            .HasColumnName("checked_in_at");

        builder.Property(r => r.CheckedInById)
            .HasColumnName("checked_in_by_id");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // La regla de "la misma persona dos veces", y está acá y no en el servicio a propósito
        // (spec 06 §3): dos peticiones simultáneas con la misma dirección pasan las dos por
        // cualquier SELECT previo, y lo único que las separa es la base.
        //
        // Es una RESTRICCIÓN y no un índice único, que en PostgreSQL no es lo mismo aunque se
        // comporten igual: un `CREATE UNIQUE INDEX` vive en pg_index y nunca aparece en
        // pg_constraint, así que una regla declarada así no se puede leer del catálogo como lo
        // que es. Y sirve además como listado por evento: su índice de respaldo lleva event_id
        // delante, que es la lectura de `07` y la del aviso de cancelación.
        builder.HasAlternateKey(r => new { r.EventId, r.EmailNormalized })
            .HasName("ux_registration_event_email");

        // El QR es la clave de una URL pública, así que una colisión le entregaría la entrada de
        // alguien a otra persona. Son 32 bytes de azar y nunca va a pasar; lo que esto compra es
        // que si pasara sea un fallo de escritura y no un cambio de titular silencioso. Su índice
        // de respaldo es además por donde entra la página de la entrada (PGTI-58).
        builder.HasAlternateKey(r => r.QrCode)
            .HasName("ux_registration_qr_code");

        // Se dicta por teléfono y se busca por él en mesa de ayuda, así que también es único: dos
        // filas con el mismo código dejarían esa búsqueda sin respuesta.
        builder.HasAlternateKey(r => r.Reference)
            .HasName("ux_registration_reference");

        // Un destinatario trae una inscripción en un evento privado y cuantas haga falta en uno
        // público, así que acá no hay unicidad: lo que hace falta es la lectura que pregunta si
        // este token ya se gastó (spec 06 §2.2).
        builder.HasIndex(r => r.CampaignRecipientId, "ix_registration_campaign_recipient");

        // Las dos lecturas del check-in, parciales porque lo que buscan es quién vino y esa es la
        // minoría de las filas: por evento, el conteo y la lista de la rifa (spec 09 §6 y §7); por
        // contacto, los eventos a los que asistió un contacto del CRM (PGTI-170).
        builder.HasIndex(r => r.EventId, "ix_registration_event_checked_in")
            .HasFilter("checked_in_at IS NOT NULL");

        builder.HasIndex(r => r.CrmContactId, "ix_registration_contact_checked_in")
            .HasFilter("checked_in_at IS NOT NULL");

        // Se apunta al destinatario, y su borrado deja la inscripción en pie con el vínculo en
        // nulo. La persona se inscribió de verdad: perder su fila porque alguien limpió una
        // campaña sería perder a quien va a presentarse en la puerta.
        builder.HasOne(r => r.CampaignRecipient)
            .WithMany()
            .HasForeignKey(r => r.CampaignRecipientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
