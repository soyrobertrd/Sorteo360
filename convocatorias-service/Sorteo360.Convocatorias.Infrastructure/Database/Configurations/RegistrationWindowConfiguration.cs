using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Convocatorias.Infrastructure.Database.Configurations;

internal sealed class RegistrationWindowConfiguration : IEntityTypeConfiguration<RegistrationWindowData>
{
    public void Configure(EntityTypeBuilder<RegistrationWindowData> builder)
    {
        builder.ToTable("registration_window", ConvocatoriasDbContext.SchemaName);

        // El evento es la clave y no apunta a nada: vive en la base de events-service.
        builder.HasKey(w => w.EventId);

        builder.Property(w => w.EventId)
            .HasColumnName("event_id")
            .ValueGeneratedNever();

        builder.Property(w => w.RegistrationsClosedAt)
            .HasColumnName("registrations_closed_at");

        builder.Property(w => w.RegistrationsClosedById)
            .HasColumnName("registrations_closed_by_id")
            .IsRequired();

        builder.Property(w => w.ReopenedAt)
            .HasColumnName("reopened_at");

        builder.Property(w => w.ReopenedById)
            .HasColumnName("reopened_by_id");
    }
}
