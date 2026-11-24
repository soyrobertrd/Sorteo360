using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class FeatureSwitchConfiguration : IEntityTypeConfiguration<FeatureSwitchData>
{
    public void Configure(EntityTypeBuilder<FeatureSwitchData> builder)
    {
        builder.ToTable("feature_switch", "events");

        builder.HasKey(x => x.Key);

        builder.Property(x => x.Key)
            .HasColumnName("key")
            .HasMaxLength(120);

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(x => x.ChangedById)
            .HasColumnName("changed_by_id");

        builder.Property(x => x.ChangedByName)
            .HasColumnName("changed_by_name")
            .HasMaxLength(200);

        builder.Property(x => x.ChangedAt)
            .HasColumnName("changed_at");
    }
}

