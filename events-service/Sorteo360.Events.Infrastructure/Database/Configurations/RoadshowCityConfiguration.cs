using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class RoadshowCityConfiguration : IEntityTypeConfiguration<RoadshowCityData>
{
    public void Configure(EntityTypeBuilder<RoadshowCityData> builder)
    {
        builder.ToTable("roadshow_city", "events");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id");

        builder.Property(c => c.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .IsRequired();

        builder.Property(c => c.Country)
            .HasColumnName("country")
            .IsRequired();

        // Sin default en la base: la migración rellenó con él las ciudades anteriores, y desde
        // entonces el valor por omisión lo pone el servicio.
        builder.Property(c => c.TimeZone)
            .HasColumnName("time_zone")
            .IsRequired();

        builder.Property(c => c.Date)
            .HasColumnName("date");

        builder.Property(c => c.DisplayOrder)
            .HasColumnName("display_order");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(c => c.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(c => c.UpdatedById)
            .HasColumnName("updated_by_id")
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        // FK hacia EventData sin navigation property en ninguno de los dos lados.
        builder.HasOne<EventData>()
            .WithMany()
            .HasForeignKey(c => c.EventId)
            .HasConstraintName("fk_roadshow_city_event")
            .OnDelete(DeleteBehavior.Restrict);

        // Filtro global que excluye registros con soft-delete.
        builder.HasQueryFilter(c => c.DeletedAt == null);

        // Un evento tiene exactamente una ciudad viva (cliente, 2026-10-06): un roadshow por tres
        // ciudades son tres eventos. Lo dice este índice y no solo el código, para que ni una ruta
        // futura ni un arreglo a mano en SQL vuelvan a armar un itinerario.
        builder.HasIndex(c => c.EventId, "ux_roadshow_city_event_id")
            .IsUnique()
            .HasFilter("deleted_at IS NULL");
    }
}
