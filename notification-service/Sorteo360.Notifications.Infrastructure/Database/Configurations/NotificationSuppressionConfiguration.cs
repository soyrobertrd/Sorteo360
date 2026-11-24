using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Notifications.Infrastructure.Database.Configurations;

internal sealed class NotificationSuppressionConfiguration : IEntityTypeConfiguration<NotificationSuppressionData>
{
    public void Configure(EntityTypeBuilder<NotificationSuppressionData> builder)
    {
        builder.ToTable("notification_suppression", NotificationsDbContext.SchemaName);

        // La dirección es la clave: una dirección está suprimida o no lo está, y un id propio solo
        // permitiría tenerla dos veces con dos motivos distintos.
        builder.HasKey(s => s.Email);

        builder.Property(s => s.Email)
            .HasColumnName("email")
            .IsRequired();

        builder.Property(s => s.Reason)
            .HasColumnName("reason")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
