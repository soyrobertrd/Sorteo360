using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Events.Core.Accounts;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class BrandingSettingConfiguration : IEntityTypeConfiguration<BrandingSettingData>
{
    public void Configure(EntityTypeBuilder<BrandingSettingData> builder)
    {
        builder.ToTable("branding_setting", "events");

        builder.HasKey(b => b.ScopeKey);

        builder.Property(b => b.ScopeKey)
            .HasColumnName("scope_key")
            .HasMaxLength(80);

        builder.Property(b => b.AccountId)
            .HasColumnName("account_id")
            .HasDefaultValue(Account.DefaultBanreservasAccountId)
            .IsRequired();

        builder.Property(b => b.EventId)
            .HasColumnName("event_id");

        builder.Property(b => b.AppName)
            .HasColumnName("app_name")
            .HasMaxLength(120);

        builder.Property(b => b.AppSubtitle)
            .HasColumnName("app_subtitle")
            .HasMaxLength(200);

        builder.Property(b => b.OrganizationName)
            .HasColumnName("organization_name")
            .HasMaxLength(200);

        builder.Property(b => b.LogoUrl)
            .HasColumnName("logo_url")
            .HasMaxLength(2000);

        builder.Property(b => b.LogoDarkUrl)
            .HasColumnName("logo_dark_url")
            .HasMaxLength(2000);

        builder.Property(b => b.PrimaryColor)
            .HasColumnName("primary_color")
            .HasMaxLength(16);

        builder.Property(b => b.AccentColor)
            .HasColumnName("accent_color")
            .HasMaxLength(16);

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}

