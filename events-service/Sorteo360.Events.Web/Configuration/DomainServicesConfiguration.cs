using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Sorteo360.Events.Core.Accounts;
using Sorteo360.Events.Core.CompanyDirectory;
using Sorteo360.Events.Core.CrmDirectory;
using Sorteo360.Events.Core.Database;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Files;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Infrastructure.Accounts;
using Sorteo360.Events.Infrastructure.Notifications;
using Sorteo360.Events.Infrastructure.Convocatorias;
using Sorteo360.Events.Infrastructure.Crm;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Infrastructure.Directories;
using Sorteo360.Events.Infrastructure.Keycloak;
using Sorteo360.Events.Infrastructure.NotarialRecords;
using Sorteo360.Events.Infrastructure.Raffles;
using Sorteo360.Platform.Files;
using Sorteo360.Platform.Http;

namespace Sorteo360.Events.Web.Configuration;

/// <summary>
/// Registra los servicios de dominio: repositorios e interfaces de aplicación.
/// </summary>
[ExcludeFromCodeCoverage]
public static class DomainServicesConfiguration
{
    /// <summary>
    /// Margen que se le suma al tamaño máximo de un archivo para fijar el techo de la petición:
    /// un envío multipart lleva bordes y cabeceras además de los bytes del archivo.
    /// </summary>
    private const long MultipartEnvelopeBytes = 1024 * 1024;

    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        // El reloj, para la lista de la puerta, que decide sobre el día de hoy. Se registra acá
        // aunque el chassis ya lo agregue al registrar un cliente de servicio: este servicio lo
        // necesita por su cuenta, no como efecto lateral de tener vecinos.
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventService>();

        var hasCrmUrl = !string.IsNullOrWhiteSpace(configuration["Services:Crm:BaseUrl"]);

        // El directorio de empresas vive en el CRM heredado. Cuando Services:Crm:BaseUrl no
        // está configurada (p. ej. Sorteo360 sin CRM), se usan implementaciones locales sin red.
        if (hasCrmUrl)
        {
            services.AddServiceClient<ICrmCompanyClient, CrmCompanyClient>(configuration, "Crm");
            services.AddServiceClient<ICrmContactClient, CrmContactClient>(configuration, "Crm");
        }
        else
        {
            services.AddSingleton<DisabledCrmClient>();
            services.AddSingleton<ICrmCompanyClient>(sp => sp.GetRequiredService<DisabledCrmClient>());
            services.AddSingleton<ICrmContactClient>(sp => sp.GetRequiredService<DisabledCrmClient>());
        }

        services.AddScoped<CrmDirectoryStatusContext>();

        services.AddOptions<CompanyDirectorySettings>()
            .Bind(configuration.GetSection(CompanyDirectorySettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<CompanyDirectoryRefreshLock>();
        services.AddScoped<ICompanyDirectory, CachedCompanyDirectory>();

        // Sin caché y sin ajustes: los contactos se consultan en vivo cada vez que se dibuja una
        // ficha, porque un teléfono de hace una semana es peor que decir que no se sabe (ADR-0015).
        services.AddScoped<ICompanyContactDirectory, CrmCompanyContactDirectory>();

        // Quién está en la puerta: la lista de check-ins de convocatorias, la única llamada de este
        // servicio a ese (spec 09 §7.3). Todo o nada: si no se puede leer entera, el sorteo no
        // arranca, en vez de arrancar con un bombo vacío sin que nadie sepa por qué.
        services.AddServiceClient<IConvocatoriasCheckInClient, ConvocatoriasCheckInClient>(configuration, "Convocatorias");
        services.AddScoped<IAttendanceSource, RosterAttendanceSource>();

        // Las pantallas del borrador leen la puerta de una copia reciente por evento; arrancar el
        // sorteo la lee en vivo por IAttendanceSource, sin pasar por acá.
        services.AddOptions<RaffleAttendanceSettings>()
            .Bind(configuration.GetSection(RaffleAttendanceSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<RaffleAttendanceRefreshLock>();
        services.AddScoped<IRecentAttendance, CachedAttendance>();

        // Sin lista externa de membresía configurada: contesta vacío y el paso se omite.
        services.AddScoped<IAwardsMembership, NoAwardsList>();

        services.AddScoped<IContactMailability, CrmContactMailability>();
        services.AddScoped<IContactNameDirectory, CrmContactNameDirectory>();

        // Los nombres de quienes operaron un sorteo, para su acta: una lectura en vivo por id, sin
        // caché, sobre el cliente del realm.
        services.AddServiceClient<IKeycloakAdminClient, KeycloakAdminClient>(configuration, "Keycloak");

        services.AddOptions<QuoteApproverSettings>()
            .Bind(configuration.GetSection(QuoteApproverSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IRaffleOperatorDirectory, KeycloakRaffleOperatorDirectory>();

        // El sorteo de un evento y su padrón de participantes.
        services.AddScoped<IBrandingRepository, BrandingRepository>();
        services.AddScoped<IFeatureSwitchRepository, FeatureSwitchRepository>();
        services.AddScoped<Sorteo360.Events.Core.Identity.ICurrentAccount, Sorteo360.Events.Web.Auth.CurrentAccountAccessor>();
        services.AddScoped<IPlanRequestRepository, PlanRequestRepository>();
        services.AddScoped<IPlanRequestService, PlanRequestService>();
        services.AddScoped<IPlanRequestNotifier, PlanRequestNotifier>();
        services.AddScoped<IAccountOnboardingRepository, AccountOnboardingRepository>();
        services.AddScoped<IAccountOnboardingService, AccountOnboardingService>();
        services.AddScoped<IAccountOnboardingNotifier, AccountOnboardingNotifier>();
        services.AddServiceClient<IKeycloakAccountProvisioner, KeycloakAccountProvisioner>(configuration, "Keycloak");
        services.AddScoped<IPlanLimitGuard, PlanLimitGuard>();
        services.Configure<PlanBillingSettings>(configuration.GetSection(PlanBillingSettings.SectionName));
        services.AddHostedService<PlanExpirationWorker>();
        services.AddScoped<IRaffleRepository, RaffleRepository>();
        services.AddScoped<IRaffleParticipantRepository, RaffleParticipantRepository>();
        services.AddScoped<IRaffleService, RaffleService>();
        services.AddSingleton<Sorteo360.Events.Web.RaffleEndpoints.RaffleWinnerNotificationWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<Sorteo360.Events.Web.RaffleEndpoints.RaffleWinnerNotificationWorker>());
        services.AddSingleton<IRaffleBadgeRenderer, RaffleBadgeRenderer>();

        // El acta en PDF (ADR-0024). Sin estado, y la fuente se registra una vez por proceso.
        services.AddSingleton<INotarialRecordRenderer, MigraDocNotarialRecordRenderer>();

        // Los avisos salen por notification-service, con la identidad propia de este servicio.
        services.AddServiceClient<INotificationClient, NotificationClient>(configuration, "Notifications");

        services.AddOptions<NotificationSettings>()
            .Bind(configuration.GetSection(NotificationSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Se valida al arrancar porque un enlace mal armado no falla por su cuenta: el correo sale
        // igual y el síntoma es una persona haciendo clic en un enlace muerto. Por qué es una
        // variable aparte de Portal__BaseUrl, en AppSettings.
        services.AddOptions<AppSettings>()
            .Bind(configuration.GetSection(AppSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<IRaffleWinnerNotifier, RaffleWinnerNotifier>();

        // Almacenamiento de archivos: el proveedor lo elige la configuración, y de acá para
        // arriba nadie sabe cuál es.
        services.AddFileStore(configuration);
        services.AddScoped<IStoredFileRepository, StoredFileRepository>();
        services.AddScoped<IFileService, FileService>();

        // Las imágenes de un evento van aparte de IEventService: documentan el
        // evento y no participan de su ciclo de vida planning/in_progress/closed.
        services.AddScoped<IEventImageRepository, EventImageRepository>();
        services.AddScoped<IEventImageService, EventImageService>();

        // Los límites viajan en la misma sección que el proveedor porque se despliegan juntos,
        // pero la regla es de este servicio: el seam solo guarda bytes.
        services.AddOptions<UploadLimits>()
            .Bind(configuration.GetSection(UploadLimits.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IUploadPolicy>(provider =>
            new UploadPolicy(provider.GetRequiredService<IOptions<UploadLimits>>().Value));

        // El techo del transporte sale del mismo número, o subir el límite no serviría de nada:
        // el servidor cortaría la petición con un 413 sin código antes de que la regla llegue a
        // mirarla, y quien sube el archivo no se enteraría de por qué.
        var maxRequestBodyBytes =
            (configuration.GetSection(UploadLimits.SectionName).Get<UploadLimits>() ?? new UploadLimits())
            .MaxSizeBytes + MultipartEnvelopeBytes;

        services.Configure<KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = maxRequestBodyBytes);
        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxRequestBodyBytes);

        return services;
    }
}
