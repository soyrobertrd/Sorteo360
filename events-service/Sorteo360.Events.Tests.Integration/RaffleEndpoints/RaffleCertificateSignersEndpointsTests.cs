using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Core.Raffles;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;
using static Sorteo360.Events.Tests.Integration.RaffleEndpoints.RaffleTestApi;

namespace Sorteo360.Events.Tests.Integration.RaffleEndpoints;

/// <summary>
/// <c>PUT …/raffle/certificate-signers</c>: los firmantes que el certificado de premio propone por
/// omisión. Son palabras impresas en un recuerdo, no evidencia, así que se editan en cualquier estado
/// del sorteo mientras el evento siga activo.
/// </summary>
public sealed class RaffleCertificateSignersEndpointsTests : IClassFixture<EventsApiDbFactory>
{
    private readonly HttpClient _client;
    private readonly RaffleTestApi _api;

    public RaffleCertificateSignersEndpointsTests(EventsApiDbFactory factory)
    {
        _client = factory.CreateClient();
        _api = new RaffleTestApi(factory, _client);
    }

    [Fact]
    public async Task Put_Returns204AndTheRaffleReturnsTheSignersInOrder_WhenTwoArrive()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        var response = await PutAsync(eventId, Signer("Ana Pérez", "Ministra de Turismo"), Signer("Luis Gómez", "Director de Ferias"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var read = await _api.GetRaffleAsync(eventId);
        read.CertificateSigners.ShouldBe(
        [
            new CertificateSignerResponse("Ana Pérez", "Ministra de Turismo"),
            new CertificateSignerResponse("Luis Gómez", "Director de Ferias"),
        ]);
    }

    public static TheoryData<string, string> InvalidBodies() => new()
    {
        { "missing signers", "{}" },
        { "null signers", """{ "signers": null }""" },
        { "three signers", """{ "signers": [{ "name": "A", "title": "A" }, { "name": "B", "title": "B" }, { "name": "C", "title": "C" }] }""" },
        { "null signer", """{ "signers": [null] }""" },
        { "name missing", """{ "signers": [{ "title": "Ministra" }] }""" },
        { "title blank", """{ "signers": [{ "name": "Ana", "title": "   " }] }""" },
        { "name too long", $$"""{ "signers": [{ "name": "{{new string('a', 101)}}", "title": "Ministra" }] }""" },
        { "title too long", $$"""{ "signers": [{ "name": "Ana", "title": "{{new string('a', 101)}}" }] }""" },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Put_Returns400AndKeepsTheStoredSigners_WhenTheBodyIsInvalid(string _, string body)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        (await PutAsync(eventId, Signer("Ana Pérez", "Ministra de Turismo"))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await _client.PutAsync(
            CertificateSignersUrl(eventId), new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await _api.GetRaffleAsync(eventId)).CertificateSigners
            .ShouldBe([new CertificateSignerResponse("Ana Pérez", "Ministra de Turismo")]);
    }

    [Fact]
    public async Task Put_Returns400NamingTheSignerAndTheField_WhenTheSecondSignerHasNoTitle()
    {
        var response = await PutAsync(Guid.NewGuid(), Signer("Ana Pérez", "Ministra"), Signer("Luis Gómez", null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>())!;
        problem.Errors.Keys.ShouldBe(["Signers[1].Title"]);
    }

    [Fact]
    public async Task Put_ReplacesTheStoredSigners_WhenItArrivesAgain()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await PutAsync(eventId, Signer("Ana Pérez", "Ministra de Turismo"), Signer("Luis Gómez", "Director de Ferias"));

        (await PutAsync(eventId, Signer("Rosa Peña", "Directora de la OPT"))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _api.GetRaffleAsync(eventId)).CertificateSigners
            .ShouldBe([new CertificateSignerResponse("Rosa Peña", "Directora de la OPT")]);
    }

    [Fact]
    public async Task Put_ClearsTheStoredSigners_WhenTheListIsEmpty()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await PutAsync(eventId, Signer("Ana Pérez", "Ministra de Turismo"));

        (await PutAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _api.GetRaffleAsync(eventId)).CertificateSigners.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_ReturnsNoSigners_WhenNoneWereStored()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();

        (await _api.GetRaffleAsync(eventId)).CertificateSigners.ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_StoresTheSignersTrimmed_WhenTheyArriveWithSpacesAndAtTheMaximumLength()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var longest = new string('a', 100);

        var response = await PutAsync(eventId, Signer($"  {longest}  ", "  Ministra de Turismo "));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await _api.GetRaffleAsync(eventId)).CertificateSigners
            .ShouldBe([new CertificateSignerResponse(longest, "Ministra de Turismo")]);
    }

    [Theory]
    [InlineData(RaffleState.Draft)]
    [InlineData(RaffleState.InProgress)]
    [InlineData(RaffleState.Completed)]
    public async Task Put_Returns204_InEveryRaffleState(string state)
    {
        // El certificado se imprime sobre todo después del sorteo: ningún estado congela a los firmantes.
        var eventId = await _api.CreateEventWithRaffleAsync();
        await _api.SetRaffleStateAsync(eventId, state);

        var response = await PutAsync(eventId, Signer("Ana Pérez", "Ministra de Turismo"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var read = await _api.GetRaffleAsync(eventId);
        read.State.ShouldBe(state);
        read.CertificateSigners.ShouldBe([new CertificateSignerResponse("Ana Pérez", "Ministra de Turismo")]);
    }

    [Fact]
    public async Task Patch_KeepsTheStoredSigners_WhenItReplacesTheRaffle()
    {
        // El PATCH no los trae, así que no los borra: viven en su propio subrecurso.
        var eventId = await _api.CreateEventWithRaffleAsync();
        await PutAsync(eventId, Signer("Ana Pérez", "Ministra de Turismo"));

        await _api.PatchRaffleAsync(eventId, ReadyToCompleteUpdate());

        (await _api.GetRaffleAsync(eventId)).CertificateSigners
            .ShouldBe([new CertificateSignerResponse("Ana Pérez", "Ministra de Turismo")]);
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task Put_Returns409EventNotEditable_WhenTheEventIsNoLongerActive(string status)
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        await PutAsync(eventId, Signer("Ana Pérez", "Ministra de Turismo"));
        await _api.SetEventStatusAsync(eventId, status);

        var response = await PutAsync(eventId, Signer("Rosa Peña", "Directora de la OPT"));

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "EVENT_NOT_EDITABLE");
        (await _api.GetRaffleAsync(eventId)).CertificateSigners
            .ShouldBe([new CertificateSignerResponse("Ana Pérez", "Ministra de Turismo")]);
    }

    [Fact]
    public async Task Put_Returns404RaffleNotFound_WhenTheEventHasNoRaffle()
    {
        var eventId = await _api.CreateEventAsync();

        var response = await PutAsync(eventId, Signer("Ana Pérez", "Ministra de Turismo"));

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "RAFFLE_NOT_FOUND");
    }

    [Fact]
    public async Task Put_Returns403_WhenTheCallerLacksRafflesWrite()
    {
        var eventId = await _api.CreateEventWithRaffleAsync();
        var body = new SetCertificateSignersRequest { Signers = [Signer("Ana Pérez", "Ministra de Turismo")] };

        var response = await _api.SendAsync(
            HttpMethod.Put, CertificateSignersUrl(eventId), JsonContent.Create(body), AuthorizationPolicies.RafflesDraw);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.GetRaffleAsync(eventId)).CertificateSigners.ShouldBeEmpty();
    }

    private static CertificateSignerRequest Signer(string? name, string? title) => new() { Name = name, Title = title };

    private Task<HttpResponseMessage> PutAsync(Guid eventId, params CertificateSignerRequest[] signers) =>
        _client.PutAsJsonAsync(CertificateSignersUrl(eventId), new SetCertificateSignersRequest { Signers = [.. signers] });

    private static string CertificateSignersUrl(Guid eventId) => $"{RaffleUrl(eventId)}/certificate-signers";
}
