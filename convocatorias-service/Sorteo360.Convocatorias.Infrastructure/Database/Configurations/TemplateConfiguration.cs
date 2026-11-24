using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Core.Templates;

namespace Sorteo360.Convocatorias.Infrastructure.Database.Configurations;

internal sealed class TemplateConfiguration : IEntityTypeConfiguration<TemplateData>
{
    public void Configure(EntityTypeBuilder<TemplateData> builder)
    {
        builder.ToTable("template", ConvocatoriasDbContext.SchemaName, t =>
        {
            // Conjuntos cerrados. El idioma lo dicta el evento y el propósito decide la regla
            // del enlace de baja: un valor inventado en cualquiera de las dos columnas dejaría
            // una plantilla que nadie sabe cuándo se manda.
            t.HasCheckConstraint(
                "ck_template_language",
                $"language IN ({Quoted(AllowedLanguage.All)})");

            t.HasCheckConstraint(
                "ck_template_purpose",
                $"purpose IN ({Quoted(TemplatePurpose.All)})");

            // Una transaccional no se puede elegir en una campaña: su lista se deriva de filas
            // que ya tenemos, nadie la selecciona. Mismo invariante que guarda la entidad de
            // dominio, acá para que ninguna migración de datos futura lo pueda saltar.
            t.HasCheckConstraint(
                "ck_template_selectable_is_campaign",
                $"is_campaign_selectable = false OR purpose = '{TemplatePurpose.Campaign}'");
        });

        // Clave natural: una plantilla es su clave en un idioma, y la unicidad que pide el
        // modelo es exactamente esa pareja. Un id sustituto más un índice único diría lo mismo
        // dos veces.
        builder.HasKey(t => new { t.TemplateKey, t.Language });

        builder.Property(t => t.TemplateKey)
            .HasColumnName("template_key")
            .IsRequired();

        builder.Property(t => t.Language)
            .HasColumnName("language")
            .IsRequired();

        builder.Property(t => t.Subject)
            .HasColumnName("subject")
            .IsRequired();

        builder.Property(t => t.BodyHtml)
            .HasColumnName("body_html")
            .IsRequired();

        builder.Property(t => t.Purpose)
            .HasColumnName("purpose")
            .IsRequired();

        builder.Property(t => t.IsCampaignSelectable)
            .HasColumnName("is_campaign_selectable")
            .IsRequired();

        // El selector filtra por esta columna en cada carga de la pantalla de campañas.
        builder.HasIndex(t => t.IsCampaignSelectable, "ix_template_is_campaign_selectable");
    }

    /// <summary>Los valores de un conjunto cerrado, entrecomillados para un <c>IN</c> de SQL.</summary>
    private static string Quoted(IReadOnlySet<string> values) =>
        string.Join(", ", values.Order(StringComparer.Ordinal).Select(value => $"'{value}'"));
}
