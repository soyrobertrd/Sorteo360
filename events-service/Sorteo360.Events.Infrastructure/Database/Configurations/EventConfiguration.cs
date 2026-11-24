using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<EventData>
{
    public void Configure(EntityTypeBuilder<EventData> builder)
    {
        builder.ToTable("event", "events", t =>
        {
            t.HasCheckConstraint("ck_event_type",
                "type IN ('Corporate', 'Christmas', 'Wedding', 'Birthday', 'Concert', 'Graduation', 'Conference', 'Other')");

            t.HasCheckConstraint("ck_event_status",
                "status IN ('planning', 'in_progress', 'closed', 'cancelled')");

            t.HasCheckConstraint("ck_event_visibility",
                "visibility IN ('public', 'private')");

            t.HasCheckConstraint("ck_event_seating_layout",
                "seating_layout IS NULL OR seating_layout IN ('Banquet', 'Auditorium', 'Conference', 'Cocktail', 'UShape', 'Classroom')");

            t.HasCheckConstraint("ck_event_dates",
                "end_date >= start_date");

            t.HasCheckConstraint("ck_event_total_cost",
                "total_cost_amount IS NULL OR total_cost_currency IS NOT NULL");

            // Conjunto cerrado de monedas.
            t.HasCheckConstraint("ck_event_total_cost_currency",
                "total_cost_currency IS NULL OR total_cost_currency IN ('USD', 'EUR')");

            t.HasCheckConstraint("ck_event_estimated_budget",
                "estimated_budget_amount IS NULL OR estimated_budget_currency IS NOT NULL");

            t.HasCheckConstraint("ck_event_estimated_budget_currency",
                "estimated_budget_currency IS NULL OR estimated_budget_currency IN ('USD', 'EUR')");

            // El presupuesto se escribe a mano, así que la base repite la regla del borde: un
            // monto no positivo no es un presupuesto sin presupuesto, es un dato malo.
            t.HasCheckConstraint("ck_event_estimated_budget_amount",
                "estimated_budget_amount IS NULL OR estimated_budget_amount > 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.AccountId)
            .HasColumnName("account_id")
            .HasDefaultValue(Account.DefaultBanreservasAccountId)
            .IsRequired();

        builder.Property(e => e.WebhookSecret)
            .HasColumnName("webhook_secret")
            .HasMaxLength(128);

        builder.Property(e => e.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .IsRequired();

        builder.Property(e => e.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(e => e.EndDate)
            .HasColumnName("end_date")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasDefaultValue("planning")
            .IsRequired();

        builder.Property(e => e.Language)
            .HasColumnName("language")
            .IsRequired();

        // Sin HasDefaultValue, a diferencia del status: el valor por omisión vive en el borde HTTP,
        // donde una creación sin visibilidad se vuelve 'private'. Una fila insertada por fuera de
        // la API tiene que decir cuál quiere, porque publicar por accidente no se deshace.
        builder.Property(e => e.Visibility)
            .HasColumnName("visibility")
            .IsRequired();

        // La columna admite nulo y no tiene valor por omisión: un evento sin montaje del salón es uno
        // en el que nadie lo eligió todavía, y eso no es lo mismo que un cóctel.
        builder.Property(e => e.SeatingLayout)
            .HasColumnName("seating_layout");

        builder.Property(e => e.ExternalUrl)
            .HasColumnName("external_url");

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.InvitedCount)
            .HasColumnName("invited_count");

        // Value object Money mapeado como tipo owned (columnas propias).
        // Presupuesto estimado: lo escribe la OPT al planificar y es opcional en todo momento.
        // No se compara con total_cost ni acá ni en el dominio; verlos diferir es justamente para
        // lo que sirve guardar los dos.
        builder.OwnsOne(e => e.EstimatedBudget, m =>
        {
            m.Property(x => x.Amount).HasColumnName("estimated_budget_amount").HasColumnType("numeric(18,4)");
            m.Property(x => x.Currency).HasColumnName("estimated_budget_currency").HasConversion<string>();
        });

        // Costo total final del evento: se calcula luego de aprobar las cotizaciones,
        // por lo que es nullable hasta que el evento se cierra.
        builder.OwnsOne(e => e.TotalCost, m =>
        {
            m.Property(x => x.Amount).HasColumnName("total_cost_amount").HasColumnType("numeric(18,4)");
            m.Property(x => x.Currency).HasColumnName("total_cost_currency").HasConversion<string>();
        });

        builder.Property(e => e.CancellationReason)
            .HasColumnName("cancellation_reason");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(e => e.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(e => e.UpdatedById)
            .HasColumnName("updated_by_id")
            .IsRequired();

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        // Token de concurrencia optimista sobre la columna de sistema xmin de PostgreSQL:
        // dos transiciones de ciclo de vida concurrentes (p. ej. cerrar vs cancelar) no
        // pueden pisarse; la segunda falla con DbUpdateConcurrencyException.
        builder.Property<uint>("xmin")
            .IsRowVersion();

        // Filtro global que excluye registros con soft-delete.
        // Se aplica automáticamente a todas las consultas sobre EventData.
        builder.HasQueryFilter(e => e.DeletedAt == null);

        // Índice parcial que acelera queries de eventos no eliminados.
        builder.HasIndex(e => new { e.Type, e.Status })
            .HasDatabaseName("ix_event_active")
            .HasFilter("deleted_at IS NULL");

        // Índice parcial para el listado por ventana de fechas (filtros
        // start_date desde/hasta y orden descendente por start_date).
        builder.HasIndex(e => e.StartDate)
            .HasDatabaseName("ix_event_start_date")
            .HasFilter("deleted_at IS NULL");

        builder.HasIndex(e => e.AccountId)
            .HasDatabaseName("ix_event_account_id")
            .HasFilter("deleted_at IS NULL");

        builder.HasOne<AccountData>()
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .HasConstraintName("fk_event_account")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
