using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// La factory de base con un transporte que apunta lo que se le pide y contesta. Va aparte porque
/// el de por omisión no contesta nunca —ver <see cref="ConvocatoriasApiDbFactory.ConfigureSendTransport"/>—
/// y porque una clase que cuelga el transporte a propósito no puede compartir host con las que
/// solo lanzan.
/// </summary>
public sealed class CampaignHandoffFactory : ConvocatoriasApiDbFactory
{
    /// <summary>El transporte simulado, el mismo para todo el host.</summary>
    public RecordingSendGateway Gateway { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureSendTransport(IServiceCollection services) =>
        services.AddSingleton<ICampaignSendGateway>(Gateway);
}
