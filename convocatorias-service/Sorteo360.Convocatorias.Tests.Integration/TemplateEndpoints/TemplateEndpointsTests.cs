using System.Net;
using System.Net.Http.Json;
using Sorteo360.Convocatorias.Core.Templates;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.TemplateEndpoints;

/// <summary>
/// La única ruta de plantillas, contra las filas que sembró la migración en un PostgreSQL real.
/// Es el primer endpoint de dominio del servicio, así que también es el primer 401 y el primer
/// 403 de verdad.
/// </summary>
public sealed class TemplateEndpointsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private const string Route = "/v1/convocatorias/templates";

    private readonly ConvocatoriasApiDbFactory _factory;

    public TemplateEndpointsTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    private async Task<IReadOnlyList<TemplateResponse>> GetAsync(string route)
    {
        var response = await _factory.CreateClient().GetAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TemplateResponse>>()
            ?? throw new InvalidOperationException("El listado llegó vacío.");
    }

    #region El listado

    [Fact]
    public async Task List_ReturnsTheFiveSeededRows_WhenUnfiltered()
    {
        var templates = await GetAsync(Route);

        templates.Select(template => template.TemplateKey).ShouldBe(
        [
            TemplateKey.DirectMessage,
            TemplateKey.EventCancelled,
            TemplateKey.RegistrationConfirmation,
            TemplateKey.RegistrationInvite,
            TemplateKey.SaveTheDate,
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheTwoCampaignKeys_WhenFilteredBySelectable()
    {
        var templates = await GetAsync($"{Route}?selectable=true");

        templates.Select(template => template.TemplateKey).ShouldBe(
            [TemplateKey.RegistrationInvite, TemplateKey.SaveTheDate], ignoreOrder: true);

        templates.ShouldAllBe(template => template.Purpose == TemplatePurpose.Campaign);
        templates.ShouldAllBe(template => template.IsCampaignSelectable);
    }

    [Fact]
    public async Task List_ReturnsTheTwoTransactionalRows_WhenUnfiltered()
    {
        var templates = await GetAsync(Route);

        templates.Count(template => template.Purpose == TemplatePurpose.Transactional).ShouldBe(2);
    }

    [Fact]
    public async Task List_ReturnsNoBodyButASubject_WhenItServes()
    {
        // Un desplegable muestra un nombre y una línea de asunto; mandar mil líneas de HTML por
        // fila es desperdicio.
        var templates = await GetAsync(Route);

        templates.ShouldAllBe(template => template.BodyHtml == null);
        templates.ShouldAllBe(template => !string.IsNullOrWhiteSpace(template.Subject));
    }

    [Fact]
    public async Task List_ReturnsSubjectsWithTheirPlaceholders_WhenFilteredBySelectable()
    {
        // La sustitución ocurre al mandar, no al listar: el selector muestra la plantilla.
        var templates = await GetAsync($"{Route}?selectable=true");

        templates.ShouldAllBe(template => template.Subject!.Contains("{{evento}}"));
    }

    [Fact]
    public async Task List_ReturnsOnlySpanishRows_WhenUnfiltered()
    {
        var templates = await GetAsync(Route);

        templates.ShouldAllBe(template => template.Language == "es");
    }

    #endregion

    #region No hay escritura

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Write_Returns405_WhenAnyWriteMethodIsUsed(string method)
    {
        // Fase 1: el conjunto lo siembra una migración de datos y nadie edita una plantilla.
        var request = new HttpRequestMessage(new HttpMethod(method), Route)
        {
            Content = JsonContent.Create(new { templateKey = "x" })
        };

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    #endregion

    #region Autorización

    [Fact]
    public async Task List_Returns401_WhenTheCallerIsAnonymous()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_Returns403_WhenTheCallerLacksTheReadRole()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsWrite);

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_Returns200_WhenTheCallerHasOnlyTheReadRole()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.ConvocatoriasRead);

        var response = await _factory.CreateClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion
}
