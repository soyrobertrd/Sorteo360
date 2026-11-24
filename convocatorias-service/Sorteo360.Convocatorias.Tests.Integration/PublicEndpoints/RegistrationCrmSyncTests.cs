using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Crm;
using Sorteo360.Convocatorias.Core.Shared;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.PublicEndpoints;

/// <summary>
/// Lo que una inscripción le hace al CRM (spec <c>06</c> §4.2) y lo que pasa cuando el CRM no
/// está (§4.3).
/// <para>
/// <b>Crear y nunca pisar.</b> Quien se inscribe con una dirección nueva entra en el CRM con su
/// consentimiento; quien lo hace con una que ya está solo queda enlazado, y el CRM se queda
/// exactamente como estaba. La regla de unicidad del cliente es lo que hace esto necesario: al
/// decretar una persona por dirección, <c>info@empresa.com</c> <i>es</i> una persona, y un
/// empleado inscribiéndose con el buzón de su empresa pisaría a quien lo tiene.
/// </para>
/// <para>
/// <b>Cada test sirve su propio evento y su propia dirección.</b> La base y los dos vecinos viven
/// toda la clase, así que compartirlos haría que la inscripción de un test fuera el "ya existe"
/// del siguiente.
/// </para>
/// </summary>
public sealed class RegistrationCrmSyncTests : IClassFixture<RegistrationPortalFactory>
{
    private readonly RegistrationPortalFactory _factory;

    public RegistrationCrmSyncTests(RegistrationPortalFactory factory) => _factory = factory;

    /// <summary>
    /// <b>Sin paso de confirmación humana</b>, a diferencia de la carga de listas: la persona
    /// escribió sus propios datos, que es la prueba de consentimiento más fuerte que hay.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsCreatedAndSyncsTheContactWithItsConsent_WhenTheAddressIsNew()
    {
        var (eventId, email) = await AnOpenEventAsync();
        _factory.Crm.ServeDefaults();

        var response = await RegistrationTestClient.RegisterAsync(
            _factory.CreateClient(),
            eventId,
            email: email,
            firstName: "Juan",
            lastName: "Pérez",
            phone: "+1 809 555 0100");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = _factory.Crm.ContactCreateBodies.ShouldHaveSingleItem();
        created.FirstName.ShouldBe("Juan");
        created.LastName.ShouldBe("Pérez");
        created.Email.ShouldBe(email);
        created.Phone.ShouldBe("+1 809 555 0100");
        created.HasConsent.ShouldBeTrue();
        created.ConsentSource.ShouldBe(ConsentSources.SelfRegistration);

        var sync = await RegistrationTestClient.CrmSyncAsync(_factory, eventId, email);
        sync.ContactId.ShouldNotBeNull();
        sync.FailedAt.ShouldBeNull();
        sync.DetailsDiffer.ShouldBeFalse();
    }

    /// <summary>
    /// Un teléfono que el CRM no aceptaría (PGTI-355) se le pide corregir a la persona en el
    /// formulario, y el CRM no recibe nada: de llegarle, contestaría 400 y la inscripción se
    /// quedaría pendiente de sincronizar para siempre.
    /// </summary>
    [Fact]
    public async Task Register_Returns400NamingPhone_WhenThePhoneBreaksTheCrmRule()
    {
        var (eventId, email) = await AnOpenEventAsync();
        _factory.Crm.ServeDefaults();

        var response = await RegistrationTestClient.RegisterAsync(
            _factory.CreateClient(),
            eventId,
            email: email,
            phone: "809.555.0100");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("errors").EnumerateObject().Select(e => e.Name).ShouldContain("Phone");
        _factory.Crm.ContactCreateBodies.ShouldNotContain(b => b.Email == email);
    }

    /// <summary>
    /// La dirección ya la ocupa alguien, así que la inscripción se enlaza y <b>el CRM no recibe
    /// ninguna escritura</b>. Consecuencia que la spec pide decir en voz alta: con la dirección
    /// ocupada, una inscripción propia no escribe nada en el CRM, punto.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsCreatedAndWritesNothingToCrm_WhenTheAddressIsAlreadyTaken()
    {
        var (eventId, _) = await AnOpenEventAsync();
        _factory.Crm.ServeDefaults();

        var response = await RegistrationTestClient.RegisterAsync(
            _factory.CreateClient(),
            eventId,
            email: CrmStub.ContactEmail,
            firstName: CrmStub.ContactFirstName,
            lastName: CrmStub.ContactLastName);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        _factory.Crm.ContactCreateBodies.ShouldBeEmpty();
        _factory.Crm.SuccessfulContactCreateCount.ShouldBe(0);

        var sync = await RegistrationTestClient.CrmSyncAsync(_factory, eventId, CrmStub.ContactEmail);
        sync.ContactId.ShouldBe(CrmStub.ContactId);
        sync.FailedAt.ShouldBeNull();
        sync.DetailsDiffer.ShouldBeFalse();
    }

    /// <summary>
    /// Lo que escribió no es lo que hay en la ficha. Se enlaza igual, no se pisa nada, y la fila
    /// queda marcada para que una persona del CRM la mire con el <c>PATCH</c> que ya existe: no
    /// sabemos si "J. Perez" es una corrección o una errata, y adivinar es lo que esta marca
    /// existe para no hacer.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsARowFlaggedForReview_WhenTheDetailsDisagreeWithTheContactOnFile()
    {
        var (eventId, _) = await AnOpenEventAsync();
        _factory.Crm.ServeDefaults();

        await RegistrationTestClient.RegisterAsync(
            _factory.CreateClient(),
            eventId,
            email: CrmStub.ContactEmail,
            firstName: "J.",
            lastName: "Perez");

        _factory.Crm.ContactCreateBodies.ShouldBeEmpty();

        var sync = await RegistrationTestClient.CrmSyncAsync(_factory, eventId, CrmStub.ContactEmail);
        sync.ContactId.ShouldBe(CrmStub.ContactId);
        sync.DetailsDiffer.ShouldBeTrue();
    }

    /// <summary>
    /// Y la comparación es indulgente con lo que no es una diferencia: quien teclea en un móvil
    /// no pone tildes y el formulario le pone las mayúsculas solo. Avisar por eso llenaría de
    /// ruido la lista que alguien tiene que revisar entera.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsAnUnflaggedRow_WhenOnlyCaseOrAccentsDiffer()
    {
        var (eventId, _) = await AnOpenEventAsync();
        _factory.Crm.ServeDefaults();

        await RegistrationTestClient.RegisterAsync(
            _factory.CreateClient(),
            eventId,
            email: CrmStub.ContactEmail,
            firstName: "ANA",
            lastName: "PEREZ");

        var sync = await RegistrationTestClient.CrmSyncAsync(_factory, eventId, CrmStub.ContactEmail);
        sync.DetailsDiffer.ShouldBeFalse();
    }

    /// <summary>
    /// Ni con un espacio de sobra. La fila se guarda recortada, así que avisar por eso mandaría a
    /// revisar a mano a una persona cuyos datos ya coinciden exactamente con la ficha.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsAnUnflaggedRow_WhenTheNameOnlyCarriesStrayPadding()
    {
        var (eventId, _) = await AnOpenEventAsync();
        _factory.Crm.ServeDefaults();

        await RegistrationTestClient.RegisterAsync(
            _factory.CreateClient(),
            eventId,
            email: CrmStub.ContactEmail,
            firstName: $" {CrmStub.ContactFirstName} ",
            lastName: $"{CrmStub.ContactLastName}  ");

        var sync = await RegistrationTestClient.CrmSyncAsync(_factory, eventId, CrmStub.ContactEmail);
        sync.DetailsDiffer.ShouldBeFalse();
    }

    /// <summary>
    /// El CRM se ocupó entre la lectura y el alta: contesta <c>409</c> con la ficha viva dentro.
    /// Es el mismo caso que la dirección ocupada, descubierto un momento después, y sale por la
    /// misma puerta — un segundo camino se separaría del primero.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsARowLinkedToTheWinner_WhenCrmRefusesTheCreateAsADuplicate()
    {
        var (eventId, email) = await AnOpenEventAsync();
        var winner = new StubContact(Guid.NewGuid(), "Ana", "Pérez", email, CanBeEmailed: true, SuppressionReason: null);

        _factory.Crm.ServeDefaults();
        _factory.Crm.ServeContactCreateConflict(winner);

        var response = await RegistrationTestClient.RegisterAsync(
            _factory.CreateClient(),
            eventId,
            email: email,
            firstName: "Ana",
            lastName: "Pérez");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var sync = await RegistrationTestClient.CrmSyncAsync(_factory, eventId, email);
        sync.ContactId.ShouldBe(winner.Id);
        sync.FailedAt.ShouldBeNull();
    }

    /// <summary>
    /// El CRM no está, y la inscripción <b>se hace igual</b>: <c>201</c>, con sus dos códigos, y
    /// la fila marcada como pendiente (spec <c>06</c> §4.3).
    /// <para>
    /// Es la única desviación de la regla del <c>503 CRM_UNAVAILABLE</c> que hay en la
    /// plataforma. Aquella se escribió para nuestro personal en una pantalla de administración;
    /// aplicada a un desconocido en un aeropuerto significa un portal del gobierno caído por un
    /// servicio del que nunca oyó hablar, y una persona que se va y no vuelve.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Register_ReturnsItsCodesAndMarksTheRowPending_WhenCrmIsDown()
    {
        var (eventId, email) = await AnOpenEventAsync();
        _factory.Crm.GoDown();

        var response = await RegistrationTestClient.RegisterAsync(_factory.CreateClient(), eventId, email: email);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("reference").GetString().ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("qrCode").GetString().ShouldNotBeNullOrWhiteSpace();

        var sync = await RegistrationTestClient.CrmSyncAsync(_factory, eventId, email);
        sync.ContactId.ShouldBeNull();
        sync.FailedAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Y el conjunto pendiente sale <b>de las dos columnas anulables y de nada más</b>: la que se
    /// sincronizó no está en él, la que no se sincronizó sí, y no hace falta ninguna columna de
    /// estado para separarlas (spec <c>06</c> §4).
    /// </summary>
    [Fact]
    public async Task Register_ReturnsAPendingSetDerivableFromTheTwoNullableColumns_WhenOneSyncFails()
    {
        var (eventId, synced) = await AnOpenEventAsync();
        var pending = Unique();
        var client = _factory.CreateClient();

        _factory.Crm.ServeDefaults();
        await RegistrationTestClient.RegisterAsync(client, eventId, email: synced);

        _factory.Crm.GoDown();
        await RegistrationTestClient.RegisterAsync(client, eventId, email: pending);

        var awaiting = await RegistrationTestClient.PendingCrmSyncEmailsAsync(_factory, eventId);

        awaiting.ShouldBe([pending]);
    }

    /// <summary>
    /// Un reenvío del mismo formulario <b>no vuelve a hablar con el CRM</b>. §4.1 ya dice que no
    /// crea fila ni pisa nada; el estado de sincronización es de la fila que ya existe, y
    /// resincronizar en cada reenvío dejaría que una caída posterior convirtiera en pendiente una
    /// inscripción que ya estaba sincronizada.
    /// </summary>
    [Fact]
    public async Task Register_ReturnsWithoutCallingCrm_WhenTheSamePersonSubmitsTwice()
    {
        var (eventId, email) = await AnOpenEventAsync();
        var client = _factory.CreateClient();

        _factory.Crm.ServeDefaults();
        await RegistrationTestClient.RegisterAsync(client, eventId, email: email);

        var contactId = (await RegistrationTestClient.CrmSyncAsync(_factory, eventId, email)).ContactId;

        _factory.Crm.ResetRequestLog();
        var repeat = await RegistrationTestClient.RegisterAsync(client, eventId, email: email);

        repeat.StatusCode.ShouldBe(HttpStatusCode.OK);
        _factory.Crm.RequestCount.ShouldBe(0);

        var sync = await RegistrationTestClient.CrmSyncAsync(_factory, eventId, email);
        sync.ContactId.ShouldBe(contactId);
    }

    #region Helpers

    /// <summary>Un evento público recién servido, y una dirección que no ha usado nadie más.</summary>
    private async Task<(Guid EventId, string Email)> AnOpenEventAsync()
    {
        var eventId = Guid.NewGuid();

        _factory.Events.ServeEvents(new StubEvent(
            eventId,
            "Roadshow Madrid 2026",
            EventsStub.PublicVisibility,
            "es",
            StubEventStatus.InProgress));

        return (eventId, Unique());
    }

    /// <summary>
    /// Una dirección que no ha usado ningún otro test. La unicidad es de la base y la base vive
    /// toda la clase, así que una dirección compartida convertiría la inscripción de un test en
    /// el "ya existe" del siguiente.
    /// </summary>
    private static string Unique() =>
        EmailAddressShape.Normalize($"p{Guid.NewGuid():N}@acme.com");

    #endregion
}
