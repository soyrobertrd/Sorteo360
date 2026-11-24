using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sorteo360.Events.Core.Events;

namespace Sorteo360.Events.Infrastructure.Database.Configurations;

internal sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplateData>
{
    public void Configure(EntityTypeBuilder<NotificationTemplateData> builder)
    {
        builder.ToTable("template", "events", t =>
        {
            // Conjunto cerrado: un idioma inventado dejaría una plantilla que ninguna búsqueda
            // encuentra y un momento del flujo que deja de avisar, en silencio.
            t.HasCheckConstraint(
                "ck_template_language",
                $"language IN ({string.Join(", ", AllowedLanguage.All.Order(StringComparer.Ordinal).Select(v => $"'{v}'"))})");
        });

        // Clave natural: una plantilla es su clave en un idioma, y la unicidad que pide el modelo
        // es exactamente esa pareja. Un id sustituto más un índice único diría lo mismo dos veces.
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
    }
}
