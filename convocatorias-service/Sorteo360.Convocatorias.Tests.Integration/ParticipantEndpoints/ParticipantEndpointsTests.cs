using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Infrastructure.Database;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.Auth;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.ParticipantEndpoints;

/// <summary>
/// La lista de participantes de un evento (spec <c>07</c> §3): una fila por persona, destinatarios
/// e inscripciones unidos por la dirección.
/// <para>
/// <b>Cada test siembra su propio evento por SQL</b>, que es lo que dejaría una campaña mandada y
/// un portal usado. Llegar ahí por la API pediría lanzar y esperar al worker, y los fallos de esas
/// otras specs aparecerían como fallos de esta lista.
/// </para>
/// </summary>
public sealed class ParticipantEndpointsTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public ParticipantEndpointsTests(ConvocatoriasApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task ListParticipants_Returns403_WhenTheCallerLacksConvocatoriasRead()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsWrite);

        var response = await client.GetAsync(Route(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Convocatorias no le pregunta a events-service si el evento existe (§5, §7), así que un id que
    /// no conoce es un evento sin nadie todavía, no un <c>404</c>.
    /// </summary>
    [Fact]
    public async Task ListParticipants_Returns200WithAnEmptyPage_WhenTheEventHasNobody()
    {
        var page = await ListAsync(Guid.NewGuid());

        page.GetProperty("items").GetArrayLength().ShouldBe(0);
        page.GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task ListParticipants_ReturnsOneRowWithBothCampaigns_WhenTwoCampaignsInvitedTheSamePerson()
    {
        var eventId = Guid.NewGuid();
        var first = await SeedCampaignAsync(eventId);
        var second = await SeedCampaignAsync(eventId);
        await SeedRecipientAsync(first, "juan@empresa.com");
        await SeedRecipientAsync(second, "Juan@Empresa.com");

        var row = Items(await ListAsync(eventId)).ShouldHaveSingleItem();

        row.GetProperty("origin").GetString().ShouldBe("invited");
        row.GetProperty("campaignIds").EnumerateArray().Select(id => id.GetGuid()).ShouldBe([first, second], ignoreOrder: true);
    }

    /// <summary>
    /// Un borrador ya tiene filas de gente a la que nadie decidió escribirle (§2). Se ven al revisar
    /// esa campaña antes de lanzarla, y en ningún otro sitio.
    /// </summary>
    [Fact]
    public async Task ListParticipants_ReturnsDraftRecipientsOnlyUnderTheirCampaignId_WhenTheCampaignIsADraft()
    {
        var eventId = Guid.NewGuid();
        var draft = await SeedUnlaunchedCampaignAsync(eventId, CampaignState.Draft);
        await SeedRecipientAsync(draft, "borrador@acme.com", sendStatus: RecipientSendStatus.Pending);
        var sent = await SeedCampaignAsync(eventId);
        await SeedRecipientAsync(sent, "enviada@acme.com");

        var everyone = Items(await ListAsync(eventId));
        var underDraft = Items(await ListAsync(eventId, $"campaignId={draft}"));

        everyone.Select(Email).ShouldBe(["enviada@acme.com"]);
        underDraft.Select(Email).ShouldBe(["borrador@acme.com"]);
        underDraft[0].GetProperty("sendStatus").GetString().ShouldBe("pending");
    }

    /// <summary>
    /// Cancelada desde borrador, la campaña nunca salió (spec 07 §2): sus destinatarios se ven solo
    /// bajo su id, igual que los de un borrador, y el CSV lee la misma unión que la lista.
    /// </summary>
    [Fact]
    public async Task ListParticipants_ReturnsItsRecipientsOnlyUnderTheirCampaignId_WhenTheCampaignWasCancelledBeforeLaunch()
    {
        var eventId = Guid.NewGuid();
        var abandoned = await SeedUnlaunchedCampaignAsync(eventId, CampaignState.Cancelled);
        await SeedRecipientAsync(abandoned, "abandonada@acme.com", sendStatus: RecipientSendStatus.Pending);
        var sent = await SeedCampaignAsync(eventId);
        await SeedRecipientAsync(sent, "enviada@acme.com");

        var everyone = Items(await ListAsync(eventId));
        var underAbandoned = Items(await ListAsync(eventId, $"campaignId={abandoned}"));
        var csv = await _factory.CreateClient().GetStringAsync(CsvRoute(eventId));
        var csvUnderAbandoned = await _factory.CreateClient().GetStringAsync($"{CsvRoute(eventId)}?campaignId={abandoned}");

        everyone.Select(Email).ShouldBe(["enviada@acme.com"]);
        underAbandoned.Select(Email).ShouldBe(["abandonada@acme.com"]);
        csv.ShouldNotContain("abandonada@acme.com");
        csvUnderAbandoned.ShouldContain("abandonada@acme.com");
    }

    /// <summary>
    /// Cancelada en pleno envío, la campaña sí salió: sus destinatarios están en la lista del evento
    /// como los de cualquier campaña lanzada.
    /// </summary>
    [Fact]
    public async Task ListParticipants_ReturnsItsRecipients_WhenTheCampaignWasCancelledAfterLaunch()
    {
        var eventId = Guid.NewGuid();
        var cut = await SeedCampaignAsync(eventId, CampaignState.Cancelled);
        await SeedRecipientAsync(cut, "mandada@acme.com");
        await SeedRecipientAsync(cut, "sin-mandar@acme.com", sendStatus: RecipientSendStatus.Pending);

        var everyone = Items(await ListAsync(eventId));

        everyone.Select(Email).ShouldBe(["mandada@acme.com", "sin-mandar@acme.com"], ignoreOrder: true);
    }

    /// <summary>
    /// La lista de una campaña es la de sus destinatarios: ni los de otra campaña del mismo evento ni
    /// quien se inscribió sin estar en ella. El estado de entrega es el de esa campaña.
    /// </summary>
    [Fact]
    public async Task ListParticipants_ReturnsOnlyThatCampaignsRecipients_WhenCampaignIdNamesASentCampaign()
    {
        var eventId = Guid.NewGuid();
        var first = await SeedCampaignAsync(eventId);
        var second = await SeedCampaignAsync(eventId);
        await SeedRecipientAsync(first, "ambas@acme.com", sendStatus: RecipientSendStatus.Failed);
        await SeedRecipientAsync(second, "ambas@acme.com");
        await SeedRecipientAsync(second, "segunda@acme.com");
        await SeedRegistrationAsync(eventId, "libre@acme.com");

        var row = Items(await ListAsync(eventId, $"campaignId={first}")).ShouldHaveSingleItem();

        Email(row).ShouldBe("ambas@acme.com");
        row.GetProperty("sendStatus").GetString().ShouldBe("failed");
    }

    [Fact]
    public async Task ListParticipants_ReturnsAWalkInWithSendStatusNone_WhenSomeoneRegisteredWithoutALink()
    {
        var eventId = Guid.NewGuid();
        await SeedRegistrationAsync(eventId, "luis@acme.com");

        var response = await _factory.CreateClient().GetAsync(Route(eventId));
        var json = await response.Content.ReadAsStringAsync();
        var row = Items(JsonDocument.Parse(json).RootElement).ShouldHaveSingleItem();

        row.GetProperty("origin").GetString().ShouldBe("walk_in");
        row.GetProperty("sendStatus").GetString().ShouldBe("none");
        row.GetProperty("registered").GetBoolean().ShouldBeTrue();
        row.GetProperty("reference").GetString().ShouldStartWith("REF-");
        row.GetProperty("referredBy").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("crmSyncPending").GetBoolean().ShouldBeTrue();

        // §5: nada de lo que todavía no se sabe, ni como campo nulo.
        foreach (var unknowable in new[] { "delivered", "opened", "clicked", "bounced" })
            json.ShouldNotContain(unknowable, Case.Insensitive);
    }

    [Fact]
    public async Task ListParticipants_ReturnsAReferredRowNamingTheInvitee_WhenSomeoneRegisteredThroughAForwardedLink()
    {
        var eventId = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(eventId);
        var invitee = await SeedRecipientAsync(campaign, "ana@empresa.com", firstName: "Ana", lastName: "Gómez");
        await SeedRegistrationAsync(eventId, "colega@empresa.com", referringRecipientId: invitee);

        var row = Items(await ListAsync(eventId, "origin=referred")).ShouldHaveSingleItem();

        row.GetProperty("email").GetString().ShouldBe("colega@empresa.com");
        row.GetProperty("referredBy").GetProperty("name").GetString().ShouldBe("Ana Gómez");
        row.GetProperty("sendStatus").GetString().ShouldBe("none");
    }

    /// <summary>
    /// <b>Dos filas es lo correcto, no un error que haya que arreglar</b> (spec <c>07</c> §3.1). La
    /// única clave es la dirección, porque el CRM decretó que una dirección es una persona:
    /// <c>juan@empresa.com</c> y <c>juan@gmail.com</c> son dos aunque se llamen igual. Por eso quien
    /// fue invitado en una y se inscribió en otra aparece como invitado sin inscribir más una entrada
    /// libre, y el conteo de no inscritos puede pasarse. Juntarlos por nombre fundiría en silencio a
    /// dos personas distintas.
    /// </summary>
    [Fact]
    public async Task ListParticipants_ReturnsTwoRows_WhenSomeoneInvitedAtOneAddressRegistersAtAnother()
    {
        var eventId = Guid.NewGuid();
        await SeedRecipientAsync(await SeedCampaignAsync(eventId), "juan@empresa.com");
        await SeedRegistrationAsync(eventId, "juan@gmail.com");

        var rows = Items(await ListAsync(eventId));

        rows.Length.ShouldBe(2);
        rows.Single(r => Email(r) == "juan@empresa.com").GetProperty("registered").GetBoolean().ShouldBeFalse();
        rows.Single(r => Email(r) == "juan@gmail.com").GetProperty("origin").GetString().ShouldBe("walk_in");
    }

    [Fact]
    public async Task ListParticipants_ReturnsEveryInvitee_WhenAskedWhoWeInvited()
    {
        var eventId = await SeedThreeQuestionsEventAsync();

        var rows = Items(await ListAsync(eventId, "origin=invited"));

        rows.Select(Email).ShouldBe(["registrada@acme.com", "pendiente@acme.com"], ignoreOrder: true);
    }

    [Fact]
    public async Task ListParticipants_ReturnsEveryRegistrantWhateverTheOrigin_WhenAskedWhoRegistered()
    {
        var eventId = await SeedThreeQuestionsEventAsync();

        var rows = Items(await ListAsync(eventId, "registered=true"));

        rows.Select(Email).ShouldBe(["registrada@acme.com", "libre@acme.com", "reenviada@acme.com"], ignoreOrder: true);
    }

    /// <summary>La entrada libre no aparece: nadie la invitó, así que no puede faltarle inscribirse (§2).</summary>
    [Fact]
    public async Task ListParticipants_ReturnsOnlyUnregisteredInvitees_WhenAskedWhoHasNotRegistered()
    {
        var eventId = await SeedThreeQuestionsEventAsync();

        var rows = Items(await ListAsync(eventId, "origin=invited&registered=false"));

        rows.Select(Email).ShouldBe(["pendiente@acme.com"]);
    }

    [Fact]
    public async Task ListParticipants_ReturnsOnlyMatchingRows_WhenSendStatusAndSearchAreSet()
    {
        var eventId = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(eventId);
        await SeedRecipientAsync(campaign, "suprimida@acme.com", sendStatus: RecipientSendStatus.Suppressed);
        await SeedRecipientAsync(campaign, "fallida@acme.com", sendStatus: RecipientSendStatus.Failed, lastName: "Peña");

        Items(await ListAsync(eventId, "sendStatus=suppressed")).Select(Email).ShouldBe(["suprimida@acme.com"]);
        Items(await ListAsync(eventId, "search=pena")).Select(Email).ShouldBe(["fallida@acme.com"]);
    }

    /// <summary>
    /// El tope de 100 del validador de siempre (§3.3), y el orden total que impide que la página dos
    /// repita una fila de la uno: todos se llaman igual, así que lo único que desempata es la dirección.
    /// </summary>
    [Fact]
    public async Task ListParticipants_ReturnsAtMost100RowsAndNoRepeatOnPageTwo_WhenTheEventHas105People()
    {
        var eventId = Guid.NewGuid();
        await SeedHundredAndFiveInviteesAsync(eventId);

        var capped = await ListAsync(eventId, "pageSize=500");
        var pageOne = Items(await ListAsync(eventId, "pageSize=100&page=1")).Select(Email).ToList();
        var pageTwo = Items(await ListAsync(eventId, "pageSize=100&page=2")).Select(Email).ToList();

        capped.GetProperty("pageSize").GetInt32().ShouldBe(100);
        capped.GetProperty("total").GetInt32().ShouldBe(105);
        Items(capped).Length.ShouldBe(100);
        pageTwo.Count.ShouldBe(5);
        pageOne.Intersect(pageTwo).ShouldBeEmpty();
        pageOne.ShouldBe(pageOne.Order(StringComparer.Ordinal));
        pageTwo[0].ShouldBe("persona101@acme.com");
    }

    #region CSV (§4)

    [Fact]
    public async Task ExportParticipantsCsv_Returns403_WhenTheCallerLacksConvocatoriasRead()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsWrite);

        var response = await client.GetAsync(CsvRoute(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>La exportación del cliente se quedaba en 100 filas sin avisar; esta no tiene tope.</summary>
    [Fact]
    public async Task ExportParticipantsCsv_ReturnsEveryRowAsAnAttachment_WhenTheEventHas105People()
    {
        var eventId = Guid.NewGuid();
        await SeedHundredAndFiveInviteesAsync(eventId);

        var response = await _factory.CreateClient().GetAsync(CsvRoute(eventId));
        var bytes = await response.Content.ReadAsByteArrayAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.ToString().ShouldBe("text/csv; charset=utf-8");
        response.Content.Headers.GetValues("Content-Disposition").ShouldHaveSingleItem()
            .ShouldMatch($$"""^attachment; filename="convocatoria-{{eventId}}-\d{4}-\d{2}-\d{2}\.csv"$""");
        bytes.Take(3).ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });
        Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(1 + 105);
    }

    /// <summary>
    /// El CSV no filtra por su cuenta: lee la misma unión que la lista. Bajo "quién se inscribió",
    /// acotado por búsqueda, las dos dan las mismas personas en el mismo orden y las mismas dos
    /// columnas de revisión al final.
    /// </summary>
    [Fact]
    public async Task ExportParticipantsCsv_ReturnsTheSameRowsAsTheList_WhenTheSameFiltersAreSet()
    {
        var eventId = await SeedThreeQuestionsEventAsync();
        const string query = "registered=true&search=acme";

        var listed = Items(await ListAsync(eventId, query))
            .Select(row => $"{Email(row)}|{Flag(row, "crmSyncPending")}|{Flag(row, "detailsDiffer")}")
            .ToList();
        var response = await _factory.CreateClient().GetAsync($"{CsvRoute(eventId)}?{query}");
        var lines = Encoding.UTF8.GetString((await response.Content.ReadAsByteArrayAsync())[3..])
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var exported = lines.Skip(1).Select(line => line.Split(',')).Select(cells => $"{cells[0]}|{cells[^3]}|{cells[^2]}");

        listed.Count.ShouldBe(3);
        exported.ShouldBe(listed);
        lines[0].ShouldEndWith("Sincronización CRM pendiente,Datos distintos,Fecha de check-in");
    }

    [Fact]
    public async Task ListCampaignRecipients_Returns403_WhenTheCallerLacksConvocatoriasRead()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.CampaignsWrite);

        var response = await client.GetAsync(CampaignRoute(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListCampaignRecipients_Returns404_WhenTheCampaignDoesNotExist()
    {
        var response = await _factory.CreateClient().GetAsync(CampaignRoute(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Por campaña o por evento con <c>campaignId</c>, una campaña de evento es la misma lista: la
    /// ruta nueva no puede cambiarle nada a la pantalla de detalle de una campaña que ya existía.
    /// </summary>
    [Fact]
    public async Task ListCampaignRecipients_ReturnsTheSameBodyAsTheEventList_WhenTheCampaignHasAnEvent()
    {
        var eventId = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(eventId);
        var other = await SeedCampaignAsync(eventId);
        var invitee = await SeedRecipientAsync(campaign, "ana@empresa.com", firstName: "Ana", lastName: "Gómez");
        await SeedRecipientAsync(campaign, "beto@empresa.com", sendStatus: RecipientSendStatus.Failed);
        await SeedRecipientAsync(other, "ana@empresa.com");
        await SeedRegistrationAsync(eventId, "ana@empresa.com", referringRecipientId: invitee);
        await SeedRegistrationAsync(eventId, "libre@empresa.com");

        var client = _factory.CreateClient();
        var byCampaign = await client.GetStringAsync(CampaignRoute(campaign));
        var byEvent = await client.GetStringAsync($"{Route(eventId)}?campaignId={campaign}");

        byCampaign.ShouldBe(byEvent);
        Items(JsonDocument.Parse(byCampaign).RootElement).Select(Email).ShouldBe(["ana@empresa.com", "beto@empresa.com"]);
    }

    [Fact]
    public async Task ListCampaignRecipients_ReturnsEveryRecipientUnregistered_WhenTheCampaignHasNoEvent()
    {
        var campaign = await SeedDirectMessageAsync();
        await SeedRecipientAsync(campaign, "zoe@acme.com", sendStatus: RecipientSendStatus.Pending, lastName: "Zapata");
        await SeedRecipientAsync(campaign, "ana@acme.com", sendStatus: RecipientSendStatus.Pending, lastName: "Alba");

        var response = await _factory.CreateClient().GetAsync(CampaignRoute(campaign));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        page.GetProperty("total").GetInt32().ShouldBe(2);
        var rows = Items(page);
        rows.Select(Email).ShouldBe(["ana@acme.com", "zoe@acme.com"]);
        rows.ShouldAllBe(row => row.GetProperty("origin").GetString() == "invited"
            && row.GetProperty("sendStatus").GetString() == "pending"
            && !row.GetProperty("registered").GetBoolean());
    }

    private static string Flag(JsonElement row, string name) => row.GetProperty(name).GetBoolean() ? "true" : "false";

    private static string CsvRoute(Guid eventId) => $"{Route(eventId)}.csv";

    #endregion

    /// <summary>
    /// Una invitada inscrita, una sin inscribir, una entrada libre y una que llegó por reenvío: las
    /// cuatro formas de estar en la lista, para las tres preguntas del cliente.
    /// </summary>
    private async Task<Guid> SeedThreeQuestionsEventAsync()
    {
        var eventId = Guid.NewGuid();
        var campaign = await SeedCampaignAsync(eventId);
        var registered = await SeedRecipientAsync(campaign, "registrada@acme.com");
        await SeedRecipientAsync(campaign, "pendiente@acme.com");
        await SeedRegistrationAsync(eventId, "registrada@acme.com", referringRecipientId: registered);
        await SeedRegistrationAsync(eventId, "libre@acme.com");
        await SeedRegistrationAsync(eventId, "reenviada@acme.com", referringRecipientId: registered);
        return eventId;
    }

    /// <summary>105 invitados que se llaman igual, así que lo único que los ordena es la dirección.</summary>
    private async Task SeedHundredAndFiveInviteesAsync(Guid eventId)
    {
        var campaign = await SeedCampaignAsync(eventId);
        await ExecuteAsync($"""
            INSERT INTO convocatorias.campaign_recipient
                (id, campaign_id, contact_id, first_name, last_name, email, send_status, created_at, updated_at)
            SELECT gen_random_uuid(), {campaign}, gen_random_uuid(), 'Juan', 'Pérez',
                   'persona' || lpad(n::text, 3, '0') || '@acme.com', 'handed_off', now(), now()
            FROM generate_series(1, 105) AS n
            """);
    }

    private static string Route(Guid eventId) => $"/v1/convocatorias/events/{eventId}/participants";

    private static string CampaignRoute(Guid campaignId) => $"/v1/convocatorias/campaigns/{campaignId}/recipients";

    private async Task<JsonElement> ListAsync(Guid eventId, string? query = null)
    {
        var response = await _factory.CreateClient().GetAsync(query is null ? Route(eventId) : $"{Route(eventId)}?{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static JsonElement[] Items(JsonElement page) => [.. page.GetProperty("items").EnumerateArray()];

    private static string Email(JsonElement row) => row.GetProperty("email").GetString()!;

    /// <summary>Una campaña lanzada, como toda la que salió de borrador por la API.</summary>
    private Task<Guid> SeedCampaignAsync(Guid eventId, string state = CampaignState.Sent) =>
        InsertCampaignAsync(eventId, state, launched: true);

    /// <summary>Un borrador, o una campaña cancelada sin haber salido nunca de borrador.</summary>
    private Task<Guid> SeedUnlaunchedCampaignAsync(Guid eventId, string state) =>
        InsertCampaignAsync(eventId, state, launched: false);

    /// <summary>La fila tal cual, con la estampa de lanzamiento entera o sin ninguna.</summary>
    private async Task<Guid> InsertCampaignAsync(Guid eventId, string state, bool launched)
    {
        var id = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();
        Guid? launcher = launched ? actor : null;
        DateTimeOffset? launchedAt = launched ? DateTimeOffset.UtcNow.AddHours(-3) : null;
        await ExecuteAsync($"""
            INSERT INTO convocatorias.campaign
                (id, event_id, template_key, language, state, launched_by_id, launched_at,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES ({id}, {eventId}, 'registration_invite', 'es', {state}, {launcher}, {launchedAt},
                    now(), now(), {actor}, {actor})
            """);
        return id;
    }

    /// <summary>Un mensaje directo en borrador: sin evento, con su asunto y su texto (PGTI-174).</summary>
    private async Task<Guid> SeedDirectMessageAsync()
    {
        var id = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7();
        await ExecuteAsync($"""
            INSERT INTO convocatorias.campaign
                (id, event_id, template_key, language, state, subject, message,
                 created_at, updated_at, created_by_id, updated_by_id)
            VALUES ({id}, NULL, 'direct_message', 'es', {CampaignState.Draft}, 'Novedades', 'Hola.',
                    now(), now(), {actor}, {actor})
            """);
        return id;
    }

    private async Task<Guid> SeedRecipientAsync(
        Guid campaignId,
        string email,
        string sendStatus = RecipientSendStatus.HandedOff,
        string firstName = "Juan",
        string lastName = "Pérez")
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync($"""
            INSERT INTO convocatorias.campaign_recipient
                (id, campaign_id, contact_id, first_name, last_name, email, send_status, created_at, updated_at)
            VALUES ({id}, {campaignId}, {Guid.CreateVersion7()}, {firstName}, {lastName}, {email}, {sendStatus}, now(), now())
            """);
        return id;
    }

    /// <summary>Una inscripción pendiente de sincronizar con el CRM, que es la que más columnas ejercita.</summary>
    private async Task SeedRegistrationAsync(Guid eventId, string email, Guid? referringRecipientId = null)
    {
        var unique = Guid.NewGuid().ToString("N");
        await ExecuteAsync($"""
            INSERT INTO convocatorias.registration
                (id, event_id, campaign_recipient_id, first_name, last_name, email, email_normalized,
                 reference, qr_code, contact_details_differ, crm_sync_failed_at, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, {eventId}, {referringRecipientId}, 'Luis', 'Peña', {email},
                    {email.Trim().ToLowerInvariant()}, {"MITUR-2026-" + unique}, {unique}, false, now(), now(), now())
            """);
    }

    private async Task ExecuteAsync(FormattableString sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConvocatoriasDbContext>();
        await context.Database.ExecuteSqlAsync(sql);
    }
}
