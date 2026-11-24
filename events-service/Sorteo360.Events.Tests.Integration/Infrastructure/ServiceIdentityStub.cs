using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sorteo360.Platform.Http;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// La identidad con la que este servicio llama al CRM, resuelta sin un Keycloak levantado.
/// <para>
/// Se sustituye solo el emisor del token: el handler que lo adjunta, la tubería de resiliencia y
/// el cliente tipado siguen siendo los reales. Cómo se obtiene un token de verdad ya está cubierto
/// en la suite del chasis, y repetirlo acá obligaría a levantar un realm por test.
/// </para>
/// </summary>
public static class ServiceIdentityStub
{
    /// <summary>
    /// Deja la configuración que <c>AddServiceClient</c> exige para registrarse —falla al arrancar
    /// si falta— y apunta los destinos <c>Crm</c> y <c>Notifications</c> a sus vecinos simulados.
    /// </summary>
    public static void UseStubbedServiceIdentity(
        this IWebHostBuilder builder,
        string crmBaseUrl,
        string notificationsBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("Services:Crm:BaseUrl", crmBaseUrl);
        builder.UseSetting("Services:Notifications:BaseUrl", notificationsBaseUrl);

        // Las dos variables del aviso. App__BaseUrl lleva un host reconocible a propósito: lo que
        // los tests comprueban del enlace es que salga de esta configuración y no de la plantilla.
        builder.UseSetting("App:BaseUrl", "https://app.pruebas.sorteo360.local");
        builder.UseSetting("Notifications:FromAddress", "no-reply@sorteo360.local");

        // El realm es el otro destino que AddServiceClient exige tener configurado para poder
        // registrarse. Apunta a una máquina que no existe a propósito: ninguna suite de acá
        // resuelve operadores, y si alguna empieza a hacerlo tiene que declararlo, no heredarlo.
        builder.UseSetting("Services:Keycloak:BaseUrl", "http://keycloak.invalid");

        // Convocatorias tampoco existe: su cliente sigue registrado, pero desde A4 la puerta es el
        // padrón (RosterAttendanceSource) y nadie lo llama.
        builder.UseSetting("Services:Convocatorias:BaseUrl", "http://convocatorias.invalid");

        builder.UseSetting("ServiceAuth:ClientId", "events-service");
        builder.UseSetting("ServiceAuth:ClientSecret", "integration-testing");
        builder.UseSetting("Keycloak:Authority", "http://keycloak.invalid/realms/sorteo360");
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
