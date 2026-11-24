using Sorteo360.Convocatorias.Core.Campaigns;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Unit.Campaigns;

/// <summary>
/// El ajuste que elige el transporte (spec <c>04</c> §4). Lo que se cobra acá es que
/// <b>no tiene valor por omisión</b>: es la diferencia entre un despliegue que se cae y una
/// campaña que informa que salió sin haberle escrito a nadie.
/// </summary>
public sealed class CampaignSendTransportTests
{
    [Theory]
    [InlineData("stub", CampaignSendTransport.Stub)]
    [InlineData("http", CampaignSendTransport.Http)]
    // Una variable de entorno la escribe una persona en un panel: se admite la grafía y el
    // espacio de más, y nada más.
    [InlineData("STUB", CampaignSendTransport.Stub)]
    [InlineData("  Http  ", CampaignSendTransport.Http)]
    public void Require_ReturnsTheTransport_WhenTheSettingNamesOne(string configured, string expected) =>
        CampaignSendTransport.Require(configured).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Require_Throws_WhenTheSettingIsMissing(string? configured)
    {
        var thrown = Should.Throw<InvalidOperationException>(() => CampaignSendTransport.Require(configured));

        // El mensaje nombra la clave, porque quien lo lee está mirando los logs de un contenedor
        // que no arranca y no el código.
        thrown.Message.ShouldContain(CampaignSendTransport.SettingKey);
    }

    [Theory]
    [InlineData("smtp")]
    [InlineData("mock")]
    [InlineData("stub ,http")]
    public void Require_Throws_WhenTheSettingIsNotATransport(string configured)
    {
        var thrown = Should.Throw<InvalidOperationException>(() => CampaignSendTransport.Require(configured));

        // Y nombra lo que se puso, para que una errata se vea sin abrir el compose.
        thrown.Message.ShouldContain(configured);
    }
}
