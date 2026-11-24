using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.CheckIns;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.Database;
using Sorteo360.Convocatorias.Core.Events;
using Sorteo360.Convocatorias.Core.Identity;
using Sorteo360.Convocatorias.Core.Participants;
using Sorteo360.Convocatorias.Core.Portal;
using Sorteo360.Convocatorias.Core.Reconciliation;
using Sorteo360.Convocatorias.Core.Registrations;
using Sorteo360.Convocatorias.Core.Rendering;
using Sorteo360.Convocatorias.Core.Tracking;
using Sorteo360.Convocatorias.Core.Unsubscribe;
using Sorteo360.Convocatorias.Core.Uploads;
using Sorteo360.Convocatorias.Infrastructure.Crm;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Infrastructure.Events;
using Sorteo360.Convocatorias.Infrastructure.Handoff;
using Sorteo360.Convocatorias.Infrastructure.Portal;
using Sorteo360.Convocatorias.Infrastructure.Uploads;
using Sorteo360.Convocatorias.Web.Auth;
using Sorteo360.Convocatorias.Web.Handoff;
using Sorteo360.Platform.Http;

namespace Sorteo360.Convocatorias.Web.Configuration;

/// <summary>
/// Registra los servicios de dominio: repositorios e interfaces de aplicación.
/// </summary>
[ExcludeFromCodeCoverage]
public static class DomainServicesConfiguration
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();

        // El reloj. Se inyecta en vez de leerse de DateTimeOffset.UtcNow en los dos sitios que
        // deciden sobre el día de hoy —si un evento todavía admite inscripciones, y el año que
        // lleva el `reference`— porque una regla que depende del calendario y no se puede fijar
        // solo se puede probar esperando a que llegue la fecha.
        services.AddSingleton(TimeProvider.System);

        // El portal de registro (spec 06 §1). Se intercala con events-service, así que no cabe
        // dentro de una transacción y no vive en el repositorio.
        services.AddScoped<IRegistrationRepository, RegistrationRepository>();
        services.AddScoped<IRegistrationService, RegistrationService>();

        // La ventana de inscripción (spec 06 §5): la leen el portal y el lanzamiento, y la
        // escriben las dos rutas que cierran y reabren.
        services.AddScoped<IRegistrationWindowRepository, RegistrationWindowRepository>();

        // El correo de confirmación de una inscripción (spec 06 §4.4). Es transaccional, así que
        // sale por ITransactionalNotifier —registrado con el mismo transporte que el de campañas,
        // en AddCampaignSendTransport— y no como una campaña. Lo dispara el endpoint tras guardar
        // la inscripción, y su fallo no la tumba.
        services.AddScoped<IRegistrationConfirmationSender, RegistrationConfirmationSender>();

        // El aviso de cancelación (spec 06 §4.7): la misma costura transaccional, a cada inscrito
        // de un evento cancelado. Se intercala con events-service, así que no vive en el repositorio.
        services.AddScoped<ICancellationNoticeService, CancellationNoticeService>();

        // La lista de participantes (spec 07 §3): destinatarios e inscripciones unidos por la
        // dirección. La unión es del servicio, para que el resumen y el CSV la reutilicen.
        services.AddScoped<IParticipantRepository, ParticipantRepository>();
        services.AddScoped<IParticipantService, ParticipantService>();

        // Los eventos a los que asistió un contacto del CRM (PGTI-170): una lectura local sobre los
        // check-ins, sin vecinos y sin nada que componer.
        services.AddScoped<IContactCheckInRepository, ContactCheckInRepository>();

        // El escaneo de la puerta y su deshecha (spec 09 §3 y §4): local, sin llamar a
        // events-service. La condición de cada escritura va dentro del UPDATE del repositorio.
        services.AddScoped<ICheckInRepository, CheckInRepository>();
        services.AddScoped<ICheckInService, CheckInService>();

        // La lista de acceso de la puerta (spec 09 §5.1): una lectura local, filtrada en memoria.
        services.AddScoped<IDoorListRepository, DoorListRepository>();
        services.AddScoped<IDoorListService, DoorListService>();

        // La lista de check-ins que lee la rifa de events-service (spec 09 §7): local, y la procedencia
        // sale de la misma regla que la de la lista de participantes.
        services.AddScoped<ICheckInListRepository, CheckInListRepository>();

        // Las campañas enviadas a un contacto del CRM (PGTI-176): la misma lectura local, por contacto.
        services.AddScoped<IContactCampaignRecipientRepository, ContactCampaignRecipientRepository>();

        // El resumen del evento (spec 07 §2): compone la unión, los cubos y la ventana, sin vecinos.
        services.AddScoped<ITrackingService, TrackingService>();

        // La pantalla de reconciliación (spec 07 §6): lecturas locales y el acto de dar por revisada
        // una ficha que no coincide.
        services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
        services.AddScoped<IReconciliationService, ReconciliationService>();
        services.AddOptions<ReconciliationSettings>()
            .Bind(configuration.GetSection(ReconciliationSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<ReconciliationSettings>>().Value);

        // La baja del portal (spec 06 §6): da de baja al contacto del token y empuja el opt-out al
        // CRM. Con el CRM caído la baja queda registrada y pendiente, y el lanzamiento la vacía por
        // el mismo camino, así que no vive en una transacción del repositorio.
        services.AddScoped<IUnsubscribeService, UnsubscribeService>();

        // El orden del lanzamiento (spec 04 §1): vaciar las bajas al CRM, recomprobar la
        // contactabilidad de la lista y recién entonces saltar a `sending`. Son tres pasos que se
        // intercalan con el CRM, así que no caben dentro de la transacción del repositorio.
        services.AddScoped<ICampaignLaunchService, CampaignLaunchService>();

        // La entrega, que corre después del 202 y sin navegador detrás (ADR-0018). El canal es
        // singleton porque es el punto de encuentro entre las peticiones que encolan y el único
        // worker que drena; el servicio que entrega una campaña es de ámbito, como el DbContext
        // que usa, y el worker le abre uno por campaña.
        services.AddSingleton<ICampaignHandoffQueue, CampaignHandoffQueue>();
        services.AddScoped<ICampaignHandoffService, CampaignHandoffService>();
        services.AddHostedService<CampaignHandoffWorker>();

        // Y por dónde sale ese lote (spec 04 §4). La elección es un valor, no una rama de entorno:
        // ver CampaignSendTransport.
        services.AddCampaignSendTransport(configuration);

        // El motor de sustitución no tiene estado ni dependencias: una instancia sirve a todo el
        // proceso.
        services.AddSingleton<ISubstitutionEngine, SubstitutionEngine>();

        // De dónde cuelgan los enlaces de la invitación. Se valida al arrancar porque un enlace
        // mal armado no falla por su cuenta: el correo sale igual y el síntoma es una persona
        // haciendo clic en un enlace muerto, días después. Se expone además el valor pelado
        // porque quien lo usa vive en Core, que no conoce IOptions.
        services.AddOptions<PortalSettings>()
            .Bind(configuration.GetSection(PortalSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(provider => provider.GetRequiredService<IOptions<PortalSettings>>().Value);

        // La imagen del QR de la entrada (spec 06 §4.5). Sin estado y solo una librería
        // administrada por dentro, así que una instancia sirve a todo el proceso; se genera al
        // vuelo, sin disco y sin volumen.
        services.AddSingleton<IQrImageRenderer, QrImageRenderer>();

        // Quién hace la petición, para el sello de lanzamiento y las columnas de auditoría.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();

        // Las dos llamadas salientes. Van por el chasis, nunca por un HttpClient pelado, así que
        // llevan la identidad propia del servicio y su tubería de resiliencia. Dónde está cada
        // destino es Services__{Nombre}__BaseUrl; quién somos es ServiceAuth__*.
        //
        // El directorio de contactos del CRM, del que sale la lista de una campaña. Si no hay
        // Services:Crm:BaseUrl configurada (Sorteo360 sin CRM), responde vacío sin tocar la red.
        if (!string.IsNullOrWhiteSpace(configuration["Services:Crm:BaseUrl"]))
            services.AddServiceClient<ICrmContactsClient, CrmContactsClient>(configuration, "Crm");
        else
            services.AddSingleton<ICrmContactsClient, DisabledCrmContactsClient>();

        services.AddScoped<ICrmContactDirectory, CrmContactDirectory>();
        services.AddScoped<IContactListReader, ContactListReader>();

        // El evento que se convoca: lo lee el lanzamiento, para no mandar un correo con un hueco,
        // y lo lee el portal, para publicar una vista recortada de él. Sin caché a propósito: una
        // lista pública cacheada es una decisión de visibilidad cacheada.
        services.AddServiceClient<IEventsClient, EventsClient>(configuration, "Events");
        services.AddScoped<IEventDirectory, EventDirectory>();

        // Las cargas de lista. Los topes no son configuración —son decisiones de la spec 05 §7,
        // una de ellas un marcador provisional con dueño— así que van con sus valores por
        // omisión y sin sección en el entorno. La política y el lector no tienen estado, así que
        // una instancia sirve a todo el proceso.
        services.AddSingleton(new SpreadsheetUploadLimits());
        services.AddSingleton<ISpreadsheetUploadPolicy, SpreadsheetUploadPolicy>();
        services.AddSingleton<ISpreadsheetParser, SpreadsheetParser>();
        services.AddScoped<ICampaignUploadRepository, CampaignUploadRepository>();
        services.AddScoped<IRecipientUploadService, RecipientUploadService>();

        return services;
    }
}
