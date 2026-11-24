using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// La colección de los tests que miden tiempos, que corren solos.
/// <para>
/// xUnit corre cada clase en paralelo con las demás, y acá hay uno que mide una carrera de
/// milisegundos contra la máquina: con el resto de la suite compitiendo por los mismos núcleos,
/// una petición puede retrasarse lo bastante como para llegar del lado equivocado del corte, y el
/// test se pondría rojo por la carga y no por el andamio. Sale barato: lo que corre en exclusiva
/// son unos segundos.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StubTimingCollection
{
    public const string Name = "stub-timing";
}

/// <summary>
/// Los tres vecinos simulados hablan HTTP de verdad y sirven la forma que dice el contrato.
/// Todavía no hay cliente tipado que los llame, así que acá se los ejercita directamente:
/// el día que exista, un fallo señala al cliente y no al doble.
/// </summary>
[Collection(StubTimingCollection.Name)]
public sealed class NeighbourStubSmokeTests
{
    /// <summary>Lo que tarda en llegar la respuesta tardía. Corto: el test la espera de verdad.</summary>
    private static readonly TimeSpan LateReply = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Lo que el cliente aguanta antes de rendirse. Muy por debajo de <see cref="LateReply"/>, y
    /// muy por encima de lo que tarda en salir una petición por una conexión ya abierta: lo que
    /// este test necesita es que la petición <b>llegue</b> antes de la línea que mueve el corte, y
    /// una máquina cargada no puede meter un segundo entre esas dos cosas.
    /// </summary>
    private static readonly TimeSpan ClientPatience = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task CrmStub_ReturnsTheContactDirectoryAndItsMailability_WhenCalled()
    {
        using var crm = new CrmStub();
        using var client = new HttpClient { BaseAddress = new Uri(crm.Url) };

        var page = await client.GetFromJsonAsync<JsonElement>("/v1/contacts");
        page.GetProperty("totalCount").GetInt32().ShouldBe(2);

        var exact = await client.GetFromJsonAsync<JsonElement>($"/v1/contacts?email={CrmStub.ContactEmail}");
        exact.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(CrmStub.ContactId);

        var mailability = await client.PostAsJsonAsync(
            "/v1/contacts/mailability",
            new { ids = new[] { CrmStub.ContactId, CrmStub.SuppressedContactId } });
        var answers = await mailability.Content.ReadFromJsonAsync<JsonElement>();

        answers[0].GetProperty("canBeEmailed").GetBoolean().ShouldBeTrue();
        answers[1].GetProperty("suppressionReason").GetString().ShouldBe(CrmStub.OptOutSuppressionReason);
    }

    [Fact]
    public async Task CrmStub_Returns409WithTheExistingContact_WhenACreateCollides()
    {
        using var crm = new CrmStub();
        crm.ServeContactCreateConflict(new StubContact(
            CrmStub.ContactId, CrmStub.ContactFirstName, CrmStub.ContactLastName, CrmStub.ContactEmail, true, null));

        using var client = new HttpClient { BaseAddress = new Uri(crm.Url) };
        var response = await client.PostAsJsonAsync(
            "/v1/contacts",
            new { firstName = "Otra", lastName = "Persona", email = CrmStub.ContactEmail });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("existingContact").GetProperty("id").GetGuid().ShouldBe(CrmStub.ContactId);
        body.GetProperty("code").GetString().ShouldBe(CrmStub.DuplicateEmailCode);
    }

    [Fact]
    public async Task EventsStub_ReturnsOnlyPublicEventsInTheListAndAnyById_WhenCalled()
    {
        using var events = new EventsStub();
        var privateEvent = new StubEvent(
            Guid.NewGuid(), "Gala privada", EventsStub.PrivateVisibility, "es", StubEventStatus.InProgress);
        events.ServeEvents(
            new StubEvent(
                EventsStub.EventId, EventsStub.EventName, EventsStub.PublicVisibility, "es", StubEventStatus.InProgress),
            privateEvent);

        using var client = new HttpClient { BaseAddress = new Uri(events.Url) };

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/events?visibility=public&active=true");
        list.GetProperty("total").GetInt32().ShouldBe(1);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/v1/events/{privateEvent.Id}");
        detail.GetProperty("visibility").GetString().ShouldBe(EventsStub.PrivateVisibility);

        var unknown = await client.GetAsync($"/v1/events/{Guid.NewGuid()}");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NotificationStub_Returns202WithOneResultPerRecipient_WhenABatchIsPosted()
    {
        using var notifications = new NotificationStub();
        notifications.Reject("no-such@@bad");

        using var client = new HttpClient { BaseAddress = new Uri(notifications.Url) };
        var response = await client.PostAsJsonAsync("/v1/notifications", new
        {
            type = "convocatorias.campaign.invite",
            language = "es",
            subject = "Te invitamos",
            body = "<html></html>",
            recipients = new[]
            {
                new { email = "juan@expedia.com" },
                new { email = "no-such@@bad" },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var results = body.GetProperty("results");

        results.GetArrayLength().ShouldBe(2);
        results[0].GetProperty("status").GetString().ShouldBe(NotificationStub.AcceptedStatus);
        results[1].GetProperty("status").GetString().ShouldBe(NotificationStub.RejectedStatus);
        results[1].GetProperty("reason").GetString().ShouldBe(NotificationStub.InvalidAddressReason);
    }

    /// <summary>
    /// La fuga de PGTI-32, vista desde acá: WireMock apunta la petición cuando termina de
    /// contestarla, así que la que el vecino lento contesta tarde aterriza en el registro ya
    /// limpio del test siguiente. En events-service costaba un fallo intermitente cada tres
    /// corridas; el andamio de este servicio es el mismo, y este test es lo que impide que lo
    /// vuelva a ser el día que alguien use <c>GoSlow</c>.
    /// </summary>
    [Fact]
    public async Task RequestLog_ReturnsNoRequest_WhenALateReplyLandsAfterTheReset()
    {
        using var crm = new CrmStub();
        using var client = new HttpClient { BaseAddress = new Uri(crm.Url), Timeout = Timeout.InfiniteTimeSpan };

        // Primero una que sí se contesta, por el mismo cliente: deja abierta la conexión que usará
        // la siguiente, que así sale al cable en el acto en vez de negociar una nueva mientras se
        // le acaba la paciencia.
        await GetContactsAsync(client, CancellationToken.None);

        crm.GoSlow(LateReply);

        // La petición que el chasis abandona por tiempo de espera mientras el vecino sigue con
        // ella en la mano.
        using var patience = new CancellationTokenSource(ClientPatience);
        await Should.ThrowAsync<OperationCanceledException>(() => GetContactsAsync(client, patience.Token));

        // Lo que hace el constructor del test siguiente, antes de pedir nada suyo.
        crm.ServeDefaults();
        crm.ResetRequestLog();

        // Y acá es donde el vecino contesta por fin, ya con el registro limpio.
        await WaitUntilLoggedAsync(crm, count: 1);

        crm.RequestCount.ShouldBe(0);
        crm.LastContactListUrl.ShouldBeNull();
    }

    [Fact]
    public async Task GoDown_Returns503_WhenTheNeighbourIsDown()
    {
        using var crm = new CrmStub();
        crm.GoDown();

        using var client = new HttpClient { BaseAddress = new Uri(crm.Url) };
        var response = await client.GetAsync("/v1/contacts");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>
    /// Pide el listado de contactos y se rinde cuando lo diga el testigo. Es <c>HttpClient</c> a
    /// pelo porque lo que se prueba acá es el andamio y no la aplicación, igual que los otros
    /// cinco tests de este archivo. La rendición es del testigo y no del <c>Timeout</c> del
    /// cliente para poder reusar el mismo cliente, que es lo que mantiene la conexión abierta
    /// entre una petición y la siguiente.
    /// </summary>
    private static Task GetContactsAsync(HttpClient client, CancellationToken cancellationToken) =>
        client.GetAsync("/v1/contacts", cancellationToken);

    /// <summary>
    /// Espera a que el servidor haya registrado tantas peticiones, sin filtrar por el corte:
    /// hasta que la respuesta tardía no ha aterrizado no hay nada que afirmar, y dormir un rato
    /// fijo haría que el test pasara por no haber esperado lo suficiente.
    /// </summary>
    private static async Task WaitUntilLoggedAsync(NeighbourStub stub, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (stub.AllRequestArrivals.Count < count)
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "el vecino nunca contestó la petición tardía");
            await Task.Delay(50);
        }
    }
}
