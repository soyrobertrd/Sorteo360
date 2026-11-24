using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// La factory con el transporte <c>http</c>: detrás de la costura queda el
/// <c>HttpCampaignSendGateway</c> de producción, hablando con <see cref="NotificationStub"/> por
/// HTTP de verdad.
/// <para>
/// Se sustituye el vecino y no el cliente, como con el CRM y con eventos: lo que hay que
/// ejercitar es la cadena entera —el cuerpo del cable, el cliente tipado, la identidad que le
/// pone el chasis y su tubería de resiliencia—, y nada de eso corre si se reemplaza la interfaz
/// por un doble.
/// </para>
/// <para>
/// Tiene host propio porque el transporte se resuelve al arrancar (spec <c>04</c> §4): no es algo
/// que un test pueda cambiar a mitad, que es exactamente lo que esa decisión buscaba.
/// </para>
/// </summary>
public sealed class HttpNotificationFactory : ConvocatoriasApiDbFactory
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Después de la base, que deja el valor en `stub`: acá se pisa con el otro transporte
        // legítimo del contrato.
        builder.UseSetting(CampaignSendTransport.SettingKey, CampaignSendTransport.Http);
    }

    /// <inheritdoc />
    /// <remarks>A propósito no hace nada: lo que se cobra es el transporte de producción.</remarks>
    protected override void ConfigureSendTransport(IServiceCollection services)
    {
    }
}
