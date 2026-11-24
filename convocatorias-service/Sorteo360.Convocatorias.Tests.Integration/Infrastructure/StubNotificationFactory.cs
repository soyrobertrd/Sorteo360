using Microsoft.Extensions.DependencyInjection;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// La factory de base <b>sin sustituir el transporte</b>: detrás de la costura queda el
/// <c>StubCampaignSendGateway</c> que registra la configuración de producción, con su tabla.
/// <para>
/// Es la única clase que lo ejercita de verdad, y por eso tiene host propio: las demás cuelgan el
/// transporte o lo doblan, y ninguna de las dos cosas vale para mirar lo que el stub escribe.
/// </para>
/// </summary>
public sealed class StubNotificationFactory : ConvocatoriasApiDbFactory
{
    /// <inheritdoc />
    /// <remarks>A propósito no hace nada: ver la clase.</remarks>
    protected override void ConfigureSendTransport(IServiceCollection services)
    {
    }
}
