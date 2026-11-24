using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Convocatorias.Core.Campaigns;

namespace Sorteo360.Convocatorias.Infrastructure.Database.Configurations;

internal sealed class CampaignExclusionConfiguration : IEntityTypeConfiguration<CampaignExclusionData>
{
    public void Configure(EntityTypeBuilder<CampaignExclusionData> builder)
    {
        builder.ToTable("campaign_exclusion", ConvocatoriasDbContext.SchemaName, t =>
            t.HasCheckConstraint(
                "ck_campaign_exclusion_reason",
                $"reason IN ({Quoted(ExclusionReason.All)})"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.CampaignId)
            .HasColumnName("campaign_id")
            .IsRequired();

        builder.Property(e => e.ContactId)
            .HasColumnName("contact_id")
            .IsRequired();

        builder.Property(e => e.FirstName)
            .HasColumnName("first_name")
            .IsRequired();

        builder.Property(e => e.LastName)
            .HasColumnName("last_name")
            .IsRequired();

        builder.Property(e => e.Email)
            .HasColumnName("email");

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .IsRequired();

        builder.Property(e => e.ExcludedAt)
            .HasColumnName("excluded_at")
            .IsRequired();

        // La misma pareja única que en destinatarios: una persona está en una campaña una vez,
        // como destinataria o como exclusión, nunca como las dos.
        builder.HasIndex(e => new { e.CampaignId, e.ContactId }, "ux_campaign_exclusion_campaign_contact")
            .IsUnique();
    }

    /// <summary>Los valores de un conjunto cerrado, entrecomillados para un <c>IN</c> de SQL.</summary>
    private static string Quoted(IReadOnlySet<string> values) =>
        string.Join(", ", values.Order(StringComparer.Ordinal).Select(value => $"'{value}'"));
}
