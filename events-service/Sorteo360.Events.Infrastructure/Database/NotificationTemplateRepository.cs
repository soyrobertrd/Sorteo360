using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Templates;

namespace Sorteo360.Events.Infrastructure.Database;

/// <summary>
/// Lee la tabla de plantillas sembrada por la migración de datos.
/// </summary>
public sealed class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly EventsDbContext _context;
    private readonly ILogger<NotificationTemplateRepository> _logger;

    public NotificationTemplateRepository(EventsDbContext context, ILogger<NotificationTemplateRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotificationTemplate?> FindAsync(
        string templateKey,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        try
        {
            var row = await _context.NotificationTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.TemplateKey == templateKey && t.Language == language,
                    cancellationToken);

            // No es un error por sí solo: quien llama puede tener otro idioma al que caer, así que
            // es quien llama el que decide si falta y lo registra como error.
            if (row is null)
            {
                _logger.LogInformation(
                    "No hay plantilla sembrada para {TemplateKey} en {Language}.",
                    templateKey, language);

                return null;
            }

            return new NotificationTemplate(row.TemplateKey, row.Language, row.Subject, row.BodyHtml);
        }
        catch (Exception exception)
        {
            // No se propaga: el único llamador es el notificador, y a esa altura la cotización ya
            // está sometida. Una base caída tiene que costar un aviso, nunca el sometimiento.
            //
            // El catch es ancho porque acotarlo a DbException dejaría escapar las averías que EF
            // envuelve en InvalidOperationException, y porque la cancelación llega como
            // TaskCanceledException: las tres significan lo mismo desde acá —no hay plantilla que
            // devolver— y ninguna puede alcanzar a quien sometió. No se apoya en que el notificador
            // también atrape: la firma de esta interfaz promete no lanzar, y la promesa se cumple
            // donde se hace.
            _logger.LogError(
                exception,
                "No se pudo leer la plantilla {TemplateKey} en {Language}; el aviso no sale.",
                templateKey, language);

            return null;
        }
    }
}
