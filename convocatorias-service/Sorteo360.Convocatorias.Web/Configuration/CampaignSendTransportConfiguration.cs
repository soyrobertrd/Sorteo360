using System.Diagnostics.CodeAnalysis;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Notifications;
using Sorteo360.Convocatorias.Infrastructure.Notifications;
using Sorteo360.Platform.Http;

namespace Sorteo360.Convocatorias.Web.Configuration;

/// <summary>
/// Enlaza el transporte de envío que declara <c>Notifications__Transport</c> (spec <c>04</c> §4).
/// <para>
/// <b>Se resuelve al arrancar y nunca por petición.</b> Que la variable falte tiene que
/// tumbar el contenedor, no aparecer como una campaña que dice que salió: por eso esto corre
/// dentro de <c>AddDomainServices</c>, antes de que el host acepte tráfico, y por eso
/// <see cref="CampaignSendTransport.Require"/> lanza en vez de devolver un valor por omisión.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public static class CampaignSendTransportConfiguration
{
    public static IServiceCollection AddCampaignSendTransport(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var transport = CampaignSendTransport.Require(configuration[CampaignSendTransport.SettingKey]);

        if (transport == CampaignSendTransport.Stub)
        {
            // De ámbito, como el DbContext en el que escriben: el worker abre un ámbito por campaña,
            // y la confirmación corre dentro del ámbito de la petición de inscripción.
            services.AddScoped<ICampaignSendGateway, StubCampaignSendGateway>();
            services.AddScoped<ITransactionalNotifier, StubTransactionalNotifier>();
            services.AddSingleton<IDeliveryStatusReader, StubDeliveryStatusReader>();

            return services;
        }

        // Los dos clientes tipados reales, campaña y transaccional. Van por el chasis, nunca por un
        // HttpClient pelado, así que llevan la identidad propia del servicio y su tubería de
        // resiliencia: dónde está notification-service es Services__Notifications__BaseUrl, y quién
        // somos es ServiceAuth__*. Los dos pegan al mismo endpoint —uno solo sirve campaña y
        // transaccional (spec 04 §2)— y se distinguen por el `type` y el `about` que arma cada uno.
        //
        // Que falte la URL base tumba el arranque desde dentro de AddServiceClient, que es el mismo
        // fail-fast que §4 le pide a la variable del transporte: un despliegue al que le falte una
        // de las dos se cae igual, en vez de aceptar inscripciones y no escribirle a nadie.
        services.AddServiceClient<ICampaignSendGateway, HttpCampaignSendGateway>(
            configuration,
            NotificationsServiceName);

        services.AddServiceClient<ITransactionalNotifier, HttpTransactionalNotifier>(
            configuration,
            NotificationsServiceName);

        // La lectura de la entrega (PGTI-177), con la misma identidad: necesita notifications:read
        // en la cuenta de servicio, y sin él se degrada a "enviado".
        services.AddServiceClient<IDeliveryStatusReader, HttpDeliveryStatusReader>(
            configuration,
            NotificationsServiceName);

        // Quién firma, validado al arrancar y solo acá: con `http` un remitente que falta es un 400
        // en cada envío, y el servicio se cae antes de aceptar campañas que no saldrían.
        services.AddOptions<NotificationSettings>()
            .Bind(configuration.GetSection(NotificationSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// El nombre del destino en <c>Services__{Nombre}__BaseUrl</c>. Es el mismo que ya usa el
    /// compose de los dos entornos.
    /// </summary>
    private const string NotificationsServiceName = "Notifications";
}
