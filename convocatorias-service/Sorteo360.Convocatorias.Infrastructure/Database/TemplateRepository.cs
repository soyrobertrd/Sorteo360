using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Platform.Database;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <inheritdoc cref="ITemplateRepository" />
public sealed class TemplateRepository : ITemplateRepository
{
    private readonly ConvocatoriasDbContext _context;
    private readonly ILogger<TemplateRepository> _logger;

    public TemplateRepository(ConvocatoriasDbContext context, ILogger<TemplateRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<TemplateSummary>, BaseError>> ListAsync(
        bool selectableOnly,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync<IReadOnlyList<TemplateSummary>>(_logger, "listar las plantillas", async () =>
        {
            var query = _context.Templates.AsNoTracking();

            if (selectableOnly)
                query = query.Where(template => template.IsCampaignSelectable);

            // La proyección deja el cuerpo fuera de la consulta y no solo de la respuesta: el
            // HTML completo de cada plantilla no llega a viajar desde PostgreSQL.
            return await query
                .OrderBy(template => template.TemplateKey)
                .ThenBy(template => template.Language)
                .Select(template => new TemplateSummary(
                    template.TemplateKey,
                    template.Language,
                    template.Purpose,
                    template.IsCampaignSelectable,
                    template.Subject))
                .ToListAsync(cancellationToken);
        });

    /// <inheritdoc />
    public Task<Result<Maybe<Template>, BaseError>> GetAsync(
        string templateKey,
        string language,
        CancellationToken cancellationToken) =>
        DbResult.TryAsync(_logger, "leer la plantilla", async () =>
        {
            var row = await _context.Templates
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    template => template.TemplateKey == templateKey && template.Language == language,
                    cancellationToken);

            return row is null
                ? Maybe<Template>.None
                : Maybe.From(new Template(
                    row.TemplateKey,
                    row.Language,
                    row.Subject,
                    row.BodyHtml,
                    row.Purpose,
                    row.IsCampaignSelectable));
        });
}
