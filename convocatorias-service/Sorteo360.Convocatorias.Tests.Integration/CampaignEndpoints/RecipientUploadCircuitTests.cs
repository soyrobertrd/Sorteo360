using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// El cortacircuitos del chasis durante una confirmación (spec <c>05</c> §6.1).
/// <para>
/// Va en su propia clase porque abrir el circuito es un efecto que dura quince segundos y la
/// tubería de resiliencia es un <b>singleton compartido</b>: dejarlo abierto en la suite general
/// haría fallar a las vecinas por un motivo que no es el suyo. Cada clase de test trae su propia
/// aplicación, así que acá el circuito nace cerrado y muere con ella.
/// </para>
/// </summary>
public sealed class RecipientUploadCircuitTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public RecipientUploadCircuitTests(ConvocatoriasApiDbFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
    }

    /// <summary>
    /// Con el circuito abierto el fallo ya no es una respuesta del CRM sino una excepción local.
    /// El chasis la traduce a <c>HttpRequestException</c>, así que llega como el mismo 503: si no
    /// lo hiciera, el modo de fallo que más esperamos saldría como un 500 y le echaría la culpa
    /// a este servicio.
    /// </summary>
    [Fact]
    public async Task Confirm_Returns503CrmUnavailableWithoutCallingTheCrm_WhenTheCircuitIsOpen()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());
        var route = $"{CampaignTestClient.Route}/{campaign.Id}/recipient-uploads";

        var file = UploadFiles.Csv(UploadFiles.Person("Nueva", "Persona", "nueva@acme.com"));

        var staged = await client.PostAsync(route, UploadFiles.AsForm(file, "lista.csv", UploadFiles.CsvContentType));
        staged.StatusCode.ShouldBe(HttpStatusCode.OK);

        _factory.Crm.GoDown();

        // Cada confirmación gasta tres intentos contra el CRM (el original y dos reintentos), así
        // que cuatro llegan al mínimo de muestreo del cortacircuitos con todo fallando.
        for (var attempt = 0; attempt < NeighbourStub.CallsToOpenCircuit; attempt++)
        {
            var failing = await client.PostAsync($"{route}/confirm", content: null);
            failing.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }

        _factory.Crm.ResetRequestLog();

        var broken = await client.PostAsync($"{route}/confirm", content: null);

        broken.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var problem = await broken.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("CRM_UNAVAILABLE");
        problem.GetProperty("completed").GetInt32().ShouldBe(0);

        // La prueba de que el circuito está abierto y no de que el CRM contestó mal: no llegó
        // ninguna petición. Es lo que impide que 500 filas por 30 segundos lleguen a ocurrir.
        _factory.Crm.RequestCount.ShouldBe(0);
    }
}
