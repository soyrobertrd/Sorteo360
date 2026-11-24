using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Platform.Http;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// La identidad con la que este servicio llama a sus vecinos, resuelta sin un Keycloak
/// levantado.
/// <para>
/// Se sustituye solo el emisor del token: el handler que lo adjunta, la tubería de
/// resiliencia y los clientes tipados siguen siendo los reales. Cómo se obtiene un token de
/// verdad ya está cubierto en la suite del chasis, y repetirlo acá obligaría a levantar un
/// realm por test.
/// </para>
/// </summary>
public static class ServiceIdentityStub
{
    /// <summary>
    /// El origen del portal público con el que se arman los enlaces de la invitación. Los tests
    /// afirman contra él, así que vive acá y no repetido en cada uno.
    /// </summary>
    public const string PortalBaseUrl = "https://app.pgti360.test";

    /// <summary>Quién firma los correos en los tests, tal como lo recibe <c>Notifications__FromAddress</c>.</summary>
    public const string FromAddress = "no-reply@sorteo360.local";

    /// <summary>El nombre visible del remitente, tal como lo recibe <c>Notifications__FromName</c>.</summary>
    public const string FromName = "MITUR Convocatorias";

    /// <summary>
    /// Deja la configuración que <c>AddServiceClient</c> exige para registrarse y apunta
    /// cada destino a su vecino simulado.
    /// </summary>
    public static void UseStubbedServiceIdentity(
        this IWebHostBuilder builder,
        string crmBaseUrl,
        string eventsBaseUrl,
        string notificationsBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("Services:Crm:BaseUrl", crmBaseUrl);
        builder.UseSetting("Services:Events:BaseUrl", eventsBaseUrl);
        builder.UseSetting("Services:Notifications:BaseUrl", notificationsBaseUrl);
        builder.UseSetting("ServiceAuth:ClientId", "convocatorias-service");
        builder.UseSetting("ServiceAuth:ClientSecret", "integration-testing");
        builder.UseSetting("Keycloak:Authority", "http://keycloak.invalid/realms/pgti360");
        builder.UseSetting("Portal:BaseUrl", PortalBaseUrl);
        builder.UseSetting("Notifications:FromAddress", FromAddress);
        builder.UseSetting("Notifications:FromName", FromName);

        // El transporte que existe. Es obligatorio y sin valor por omisión, así que sin esta
        // línea ningún host de test arranca — que es exactamente lo que tiene que pasar en un
        // despliegue al que le falte la variable.
        //
        // Lo que queda detrás de la costura es el StubCampaignSendGateway de verdad, con su
        // tabla: las clases que necesitan un doble lo sustituyen ellas (CampaignHandoffFactory),
        // y la que quiere el cliente tipado contra NotificationStub pisa este valor con "http"
        // (HttpNotificationFactory).
        builder.UseSetting("Notifications:Transport", CampaignSendTransport.Stub);
    }

    /// <summary>Reemplaza el emisor de tokens por uno que siempre entrega el mismo.</summary>
    public static void AddStubbedServiceToken(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IServiceTokenProvider>();
        services.AddSingleton<IServiceTokenProvider, StubTokenProvider>();
    }

    private sealed class StubTokenProvider : IServiceTokenProvider
    {
        public Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken) =>
            Task.FromResult("integration-testing-token");
    }
}
