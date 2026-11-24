using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sorteo360.Notifications.Core.Database;
using Sorteo360.Notifications.Core.Notifications;
using Sorteo360.Notifications.Core.Rendering;
using Sorteo360.Notifications.Core.Sending;
using Sorteo360.Notifications.Core.Webhooks;
using Sorteo360.Notifications.Infrastructure.Database;
using Sorteo360.Notifications.Infrastructure.Smtp;
using Sorteo360.Notifications.Web.Workers;

namespace Sorteo360.Notifications.Web.Configuration;

/// <summary>
/// Registra los servicios de dominio: repositorios e interfaces de aplicación.
/// </summary>
[ExcludeFromCodeCoverage]
public static class DomainServicesConfiguration
{
    public static IServiceCollection AddDomainServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Si existe el webhook de eventos se lee una vez al arrancar y se estampa en cada envío
        // al aceptarlo, para que una fila escrita sin webhook siga diciendo la verdad después.
        services.AddOptions<SendGridEventWebhookSettings>()
            .Bind(configuration.GetSection(SendGridEventWebhookSettings.SectionName));

        // El motor de sustitución no tiene estado: una sola instancia.
        services.AddSingleton<ISubstitutionEngine, SubstitutionEngine>();

        services.AddScoped<INotificationBatchRepository, NotificationBatchRepository>();
        services.AddScoped<INotificationService, NotificationService>();

        // El historial solo lee: no hay servicio de aplicación entre el endpoint y el lector
        // porque no hay ninguna regla de negocio que aplicar encima de una consulta.
        services.AddScoped<INotificationHistoryReader, NotificationHistoryReader>();

        // El reloj entra por inyección: la ventana de una hora y el retroceso exponencial se
        // prueban moviendo el tiempo, no esperándolo.
        services.TryAddSingleton(TimeProvider.System);

        // La salida SMTP, con su freno de arranque: con Smtp__AllowExternalRelay en false y una
        // contraseña no vacía el host no levanta.
        services.AddSmtpSending(configuration);

        // El webhook de eventos: guardar el lote y recalcular las filas que toca, todo en una
        // transacción. La verificación de la firma es pura y vive en Core, así que no se registra.
        services.AddScoped<INotificationEventIngest, NotificationEventIngest>();

        services.AddScoped<ISendQueue, SendQueue>();
        services.AddScoped<ISuppressionList, SuppressionList>();
        services.AddScoped<INotificationDrainService, NotificationDrainService>();

        // El worker no corre en los tests de integración, por el mismo motivo por el que allí no
        // se aplican las migraciones en el arranque: el test que quiere drenar llama al servicio de
        // drenaje él mismo y comprueba el resultado, en vez de dormir esperando una vuelta.
        if (!environment.IsEnvironment("IntegrationTesting"))
            services.AddHostedService<NotificationDrainWorker>();

        return services;
    }
}
