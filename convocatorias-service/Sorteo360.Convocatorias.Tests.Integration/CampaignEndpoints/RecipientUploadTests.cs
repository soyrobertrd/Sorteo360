using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Uploads;
using Sorteo360.Convocatorias.Tests.Integration.Infrastructure;
using Sorteo360.Convocatorias.Web.CampaignEndpoints;
using Shouldly;

namespace Sorteo360.Convocatorias.Tests.Integration.CampaignEndpoints;

/// <summary>
/// Subir una lista a una campaña en borrador y confirmarla (spec <c>05</c>). La cadena entera va
/// por HTTP contra el CRM simulado: el archivo llega como multipart, se parsea de verdad, y las
/// altas salen por el cliente tipado y la tubería de resiliencia del chasis.
/// </summary>
public sealed class RecipientUploadTests : IClassFixture<ConvocatoriasApiDbFactory>
{
    private readonly ConvocatoriasApiDbFactory _factory;

    public RecipientUploadTests(ConvocatoriasApiDbFactory factory)
    {
        _factory = factory;
        _factory.Crm.ServeDefaults();
    }

    /// <summary>
    /// La primera línea de la definición de hecho: los dos formatos revisan, y las siete cuentas
    /// suman las filas de datos del archivo.
    /// </summary>
    [Theory]
    [InlineData("xlsx")]
    [InlineData("csv")]
    public async Task Stage_ReturnsSevenCountsThatAddUpToTheDataRows_WhenTheFormatIsSupported(string format)
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var rows = new[]
        {
            UploadFiles.Person("Ana", "Pérez", CrmStub.ContactEmail),
            UploadFiles.Person("Luis", "Gómez", CrmStub.SuppressedContactEmail),
            UploadFiles.Person("Nueva", "Persona", "nueva@acme.com"),
            UploadFiles.Person("Sin", "Correo", null!),
        };

        var upload = await StageAsync(client, campaign.Id, Build(format, rows), format);

        upload.RowCount.ShouldBe(4);
        Total(upload.Counts).ShouldBe(upload.RowCount);

        upload.Counts.Matched.ShouldBe(1);
        upload.Counts.Suppressed.ShouldBe(1);
        upload.Counts.Unmatched.ShouldBe(1);
        upload.Counts.Malformed.ShouldBe(1);

        // Dos, no tres: la fila suprimida sí escribe algo al confirmar, pero lo que escribe es
        // una exclusión, y §4 la marca como no confirmable por eso mismo.
        upload.ConfirmableCount.ShouldBe(2);

        upload.Rows.Count.ShouldBe(4);
        upload.Rows.Single(row => row.Bucket == UploadRowBucket.Malformed)
            .ReasonCode.ShouldBe(MalformedRowReason.MissingEmail);
        upload.Rows.Single(row => row.Bucket == UploadRowBucket.Suppressed)
            .ReasonCode.ShouldBe(ExclusionReason.OptedOut);
    }

    /// <summary>
    /// La firma sola no distingue un docx de un xlsx: los dos empiezan por los mismos cuatro
    /// bytes. Lo que los separa es la comprobación estructural.
    /// </summary>
    [Fact]
    public async Task Stage_Returns400FileTypeNotAllowed_WhenADocxIsRenamedXlsx()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await PostAsync(client, campaign.Id, UploadFiles.Docx(), "lista.xlsx", UploadFiles.XlsxContentType);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).ShouldBe("FILE_TYPE_NOT_ALLOWED");
    }

    [Fact]
    public async Task Stage_Returns400ExpandedTooLarge_WhenTheFileIsAZipBomb()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var bomb = UploadFiles.ZipBomb(new SpreadsheetUploadLimits().MaxDecompressedBytes + (8L * 1024 * 1024));
        var response = await PostAsync(client, campaign.Id, bomb, "lista.xlsx", UploadFiles.XlsxContentType);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).ShouldBe("UPLOAD_EXPANDED_TOO_LARGE");
    }

    /// <summary>Una fila por encima del tope, y el mensaje dice cuál es el tope.</summary>
    [Fact]
    public async Task Stage_Returns400TooManyRowsNamingTheCap_WhenTheFileIsOneRowPastIt()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var cap = new SpreadsheetUploadLimits().MaxDataRows;
        var rows = Enumerable.Range(0, cap + 1)
            .Select(index => UploadFiles.Person($"Nombre{index}", $"Apellido{index}", $"p{index}@acme.com"))
            .ToArray();

        var response = await PostAsync(client, campaign.Id, UploadFiles.Csv(rows), "lista.csv", UploadFiles.CsvContentType);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("UPLOAD_TOO_MANY_ROWS");
        problem.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain(cap.ToString());
    }

    /// <summary>
    /// El respaldo de codificación se anuncia. Quien vea "windows-1252" al lado de un nombre con
    /// la tilde rota tiene por dónde empezar; un respaldo en silencio escribe mojibake en el CRM.
    /// </summary>
    [Fact]
    public async Task Stage_ReturnsTheDetectedEncodingAndReadableNames_WhenTheFileIsWindows1252()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var file = UploadFiles.Csv(
            Encoding.GetEncoding(1252),
            ';',
            UploadFiles.Person("José", "Martínez", "jose@acme.com"));

        var upload = await StageAsync(client, campaign.Id, file, "csv");

        upload.DetectedEncoding.ShouldBe(SpreadsheetFormat.Windows1252);
        upload.Rows[0].FirstName.ShouldBe("José");
        upload.Rows[0].LastName.ShouldBe("Martínez");
    }

    /// <summary>
    /// Un archivo entero mal formado es una subida <b>correcta</b> con cero filas confirmables:
    /// es así como alguien descubre que se equivocó de columnas, y un 400 no se lo diría.
    /// </summary>
    [Fact]
    public async Task Stage_ReturnsNothingConfirmable_WhenEveryRowIsMalformed()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var file = UploadFiles.Csv(
            UploadFiles.Person("Ana", "Pérez", "no-es-un-correo"),
            UploadFiles.Person("Luis", "Gómez", null!),
            new UploadFiles.Row(null, null, "sin.nombre@acme.com"));

        var upload = await StageAsync(client, campaign.Id, file, "csv");

        upload.Counts.Malformed.ShouldBe(3);
        upload.ConfirmableCount.ShouldBe(0);
        Total(upload.Counts).ShouldBe(3);

        upload.Rows.Select(row => row.ReasonCode).ShouldBe(
            [MalformedRowReason.InvalidEmail, MalformedRowReason.MissingEmail, MalformedRowReason.MissingName],
            ignoreOrder: true);
    }

    /// <summary>
    /// Los dos desenlaces de §5: lo idéntico se pliega y se cuenta, y lo que se contradice va
    /// entero al cubo nombrando sus filas para que alguien vaya a mirarlas.
    /// </summary>
    [Fact]
    public async Task Stage_ReturnsCollapsedAndRowNumberedDuplicates_WhenRowsRepeatAnEmail()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var file = UploadFiles.Csv(
            UploadFiles.Person("Ana", "Pérez", "ana.p@acme.com"),
            UploadFiles.Person("Ana", "Pérez", "ana.p@acme.com"),
            UploadFiles.Person("Ana", "Pérez", "ana.p@acme.com"),
            UploadFiles.Person("Luis", "Gómez", "luis@acme.com"),
            UploadFiles.Person("Luis", "Gómez Díaz", "luis@acme.com"),
            UploadFiles.Person("Luis Alberto", "Gómez", "luis@acme.com"));

        var upload = await StageAsync(client, campaign.Id, file, "csv");

        upload.RowCount.ShouldBe(6);
        Total(upload.Counts).ShouldBe(6);
        upload.Counts.CollapsedExactDuplicates.ShouldBe(2);
        upload.Counts.DuplicateInFile.ShouldBe(3);

        var duplicates = upload.Rows.Where(row => row.Bucket == UploadRowBucket.DuplicateInFile).ToList();
        duplicates.Count.ShouldBe(3);
        duplicates.ShouldAllBe(row => row.DuplicateRowNumbers.Count == 3);
        duplicates[0].DuplicateRowNumbers.ShouldBe([5, 6, 7]);

        upload.Rows.Single(row => row.CollapsedDuplicates > 0).CollapsedDuplicates.ShouldBe(2);
    }

    /// <summary>
    /// La confirmación: crea solo lo nuevo, enlaza lo encontrado, escribe una exclusión por lo
    /// suprimido, y las filas revisadas se van dejando el resumen.
    /// </summary>
    [Fact]
    public async Task Confirm_ReturnsCreatedUnmatchedLinkedMatchedAndExcludedSuppressed_WhenStaged()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var file = UploadFiles.Csv(
            UploadFiles.Person("Ana", "Pérez", CrmStub.ContactEmail),
            UploadFiles.Person("Luis", "Gómez", CrmStub.SuppressedContactEmail),
            UploadFiles.Person("Nueva", "Persona", "nueva@acme.com"),
            UploadFiles.Person("Otra", "Nueva", "otra@acme.com"));

        await StageAsync(client, campaign.Id, file, "csv");

        var confirmed = await ConfirmAsync(client, campaign.Id);

        confirmed.ConfirmedAt.ShouldNotBeNull();
        confirmed.ConfirmedById.ShouldBe(TestAuthHandler.DefaultUserId);
        confirmed.Counts.Matched.ShouldBe(1);
        confirmed.Counts.Unmatched.ShouldBe(2);
        confirmed.Counts.Suppressed.ShouldBe(1);
        confirmed.Rows.ShouldBeEmpty();

        _factory.Crm.SuccessfulContactCreateCount.ShouldBe(2);

        var emails = await CampaignTestClient.RecipientEmailsAsync(_factory, campaign.Id);
        emails.ShouldBe([CrmStub.ContactEmail, "nueva@acme.com", "otra@acme.com"], ignoreOrder: true);

        (await CampaignTestClient.CountExclusionsAsync(_factory, campaign.Id, ExclusionReason.OptedOut)).ShouldBe(1);

        // El bulto se fue y el lote se quedó: seis enteros y dos identidades contestan lo mismo.
        (await CampaignTestClient.CountUploadRowsAsync(_factory, campaign.Id)).ShouldBe(0);
        (await CampaignTestClient.CountUploadsAsync(_factory, campaign.Id)).ShouldBe(1);
        (await CampaignTestClient.CountPendingUploadsAsync(_factory, campaign.Id)).ShouldBe(0);
    }

    /// <summary>
    /// Un teléfono de la hoja que el CRM no aceptaría (PGTI-355) no se le manda: la hoja no se
    /// puede corregir desde la pantalla, y perder el teléfono es mejor que perder el contacto.
    /// Uno que sí cumple viaja tal cual.
    /// </summary>
    [Fact]
    public async Task Confirm_ReturnsTheContactCreatedWithoutAPhone_WhenTheSheetPhoneBreaksTheCrmRule()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());
        var malformed = $"malo-{Guid.NewGuid():N}@acme.com";
        var wellFormed = $"bueno-{Guid.NewGuid():N}@acme.com";

        await StageAsync(client, campaign.Id, UploadFiles.Csv(
            new UploadFiles.Row("Telefono", "Malo", malformed, Phone: "809.555.0100 ext 4"),
            new UploadFiles.Row("Telefono", "Bueno", wellFormed, Phone: "+1 (809) 555-0100")), "csv");

        var confirmed = await ConfirmAsync(client, campaign.Id);

        confirmed.Counts.Unmatched.ShouldBe(2);
        _factory.Crm.ContactCreateBodies.Single(b => b.Email == malformed).Phone.ShouldBeNull();
        _factory.Crm.ContactCreateBodies.Single(b => b.Email == wellFormed).Phone.ShouldBe("+1 (809) 555-0100");
    }

    /// <summary>
    /// Alguien que ya es destinatario no se toca: <c>already_a_recipient</c> gana a
    /// <c>suppressed</c>, y confirmar no reescribe su fila.
    /// </summary>
    [Fact]
    public async Task Stage_ReturnsAlreadyARecipientAndConfirmLeavesItAlone_WhenTheRowIsARecipient()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        // La selección materializa al contactable del directorio; la hoja lo vuelve a traer.
        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var upload = await StageAsync(
            client,
            campaign.Id,
            UploadFiles.Csv(UploadFiles.Person("Ana", "Pérez", CrmStub.ContactEmail)),
            "csv");

        upload.Counts.AlreadyARecipient.ShouldBe(1);
        upload.ConfirmableCount.ShouldBe(0);

        await ConfirmAsync(client, campaign.Id);

        (await CampaignTestClient.RecipientIdsAsync(_factory, campaign.Id)).Count.ShouldBe(1);
        _factory.Crm.SuccessfulContactCreateCount.ShouldBe(0);
    }

    /// <summary>
    /// El CRM se cae a mitad: la confirmación se para, dice cuántas filas completó, y el lote
    /// sigue vivo. Reintentar crea solo el resto, que es lo que prueba que las primeras no se
    /// crean dos veces.
    /// </summary>
    [Fact]
    public async Task Confirm_Returns503WithTheCompletedCountAndARetryFinishes_WhenTheCrmFailsPartway()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        const int total = 35;
        const int beforeTheFall = 29;

        var rows = Enumerable.Range(0, total)
            .Select(index => UploadFiles.Person($"Nombre{index}", $"Apellido{index}", $"p{index}@acme.com"))
            .ToArray();

        await StageAsync(client, campaign.Id, UploadFiles.Csv(rows), "csv");

        _factory.Crm.FailContactCreateAfter(beforeTheFall);

        var refused = await client.PostAsync(UploadRoute(campaign.Id) + "/confirm", content: null);

        refused.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("CRM_UNAVAILABLE");
        problem.GetProperty("completed").GetInt32().ShouldBe(beforeTheFall);

        (await CampaignTestClient.RecipientIdsAsync(_factory, campaign.Id)).Count.ShouldBe(beforeTheFall);
        (await CampaignTestClient.CountPendingUploadsAsync(_factory, campaign.Id)).ShouldBe(1);

        _factory.Crm.HealContactCreate();

        var confirmed = await ConfirmAsync(client, campaign.Id);

        confirmed.Counts.Unmatched.ShouldBe(total);
        (await CampaignTestClient.RecipientIdsAsync(_factory, campaign.Id)).Count.ShouldBe(total);

        // Ni uno más: las 29 primeras no se volvieron a crear en el reintento.
        _factory.Crm.SuccessfulContactCreateCount.ShouldBe(total);
    }

    /// <summary>
    /// Un 409 es una coincidencia, no un fallo. Y hay que volver a mirar el <c>canBeEmailed</c>
    /// del contacto que había detrás: sin eso, la carrera le escribe a quien se dio de baja.
    /// </summary>
    [Fact]
    public async Task Confirm_ReturnsTheRowExcluded_WhenTheCrmConflictIsASuppressedContact()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        // La dirección no está en el directorio, así que la revisión la ve nueva.
        await StageAsync(
            client,
            campaign.Id,
            UploadFiles.Csv(UploadFiles.Person("Tarde", "Baja", "tarde@acme.com")),
            "csv");

        // Entre revisar y confirmar, esa dirección resulta ser de alguien que se dio de baja.
        var suppressed = new StubContact(
            Guid.NewGuid(), "Tarde", "Baja", "tarde@acme.com", CanBeEmailed: false, CrmStub.OptOutSuppressionReason);

        _factory.Crm.ServeContactCreateConflict(suppressed);

        var confirmed = await ConfirmAsync(client, campaign.Id);

        confirmed.Counts.Unmatched.ShouldBe(0);
        confirmed.Counts.Suppressed.ShouldBe(1);

        (await CampaignTestClient.RecipientIdsAsync(_factory, campaign.Id)).ShouldBeEmpty();
        (await CampaignTestClient.CountExclusionsAsync(_factory, campaign.Id, ExclusionReason.OptedOut)).ShouldBe(1);
    }

    /// <summary>
    /// Un 409 sobre alguien contactable sí enlaza, sin crear nada, y la fila pasa de "nueva" a
    /// "encontrada" en el resumen.
    /// </summary>
    [Fact]
    public async Task Confirm_ReturnsTheRowLinkedWithoutCreating_WhenTheCrmConflictIsAMailableContact()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        await StageAsync(
            client,
            campaign.Id,
            UploadFiles.Csv(UploadFiles.Person("Ya", "Estaba", "yaestaba@acme.com")),
            "csv");

        var existing = new StubContact(
            Guid.NewGuid(), "Ya", "Estaba", "yaestaba@acme.com", CanBeEmailed: true, SuppressionReason: null);

        _factory.Crm.ServeContactCreateConflict(existing);

        var confirmed = await ConfirmAsync(client, campaign.Id);

        confirmed.Counts.Matched.ShouldBe(1);
        confirmed.Counts.Unmatched.ShouldBe(0);

        (await CampaignTestClient.RecipientEmailsAsync(_factory, campaign.Id)).ShouldBe(["yaestaba@acme.com"]);
    }

    /// <summary>
    /// Subir dos veces reemplaza: un solo sitio de carga pendiente por campaña, y el índice
    /// parcial lo sostiene en la base.
    /// </summary>
    [Fact]
    public async Task Stage_ReturnsOnlyTheSecondBatchPending_WhenUploadedTwiceOntoOneDraft()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        await StageAsync(client, campaign.Id, UploadFiles.Csv(
            UploadFiles.Person("Primera", "Lista", "primera@acme.com")), "csv");

        var second = await StageAsync(client, campaign.Id, UploadFiles.Csv(
            UploadFiles.Person("Segunda", "Lista", "segunda@acme.com"),
            UploadFiles.Person("Otra", "Más", "otra@acme.com")), "csv");

        second.RowCount.ShouldBe(2);

        (await CampaignTestClient.CountPendingUploadsAsync(_factory, campaign.Id)).ShouldBe(1);
        (await CampaignTestClient.CountUploadsAsync(_factory, campaign.Id)).ShouldBe(1);
        (await CampaignTestClient.CountUploadRowsAsync(_factory, campaign.Id)).ShouldBe(2);
    }

    /// <summary>
    /// Confirmar libera el sitio, que es como se componen dos listas de verdad, y la carga
    /// confirmada se queda: es el registro de quién confirmó.
    /// </summary>
    [Fact]
    public async Task Confirm_ReturnsAFreeSlotForASecondList_WhenTheFirstIsConfirmed()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        await StageAsync(client, campaign.Id, UploadFiles.Csv(
            UploadFiles.Person("Primera", "Lista", "primera@acme.com")), "csv");
        await ConfirmAsync(client, campaign.Id);

        await StageAsync(client, campaign.Id, UploadFiles.Csv(
            UploadFiles.Person("Segunda", "Lista", "segunda@acme.com")), "csv");
        await ConfirmAsync(client, campaign.Id);

        (await CampaignTestClient.CountUploadsAsync(_factory, campaign.Id)).ShouldBe(2);
        (await CampaignTestClient.RecipientEmailsAsync(_factory, campaign.Id))
            .ShouldBe(["primera@acme.com", "segunda@acme.com"], ignoreOrder: true);
    }

    /// <summary>
    /// La guarda de borrador es la misma que hace de solo lectura una campaña en <c>sending</c>.
    /// Desde que se retiró la aprobación es la regla entera.
    /// </summary>
    [Fact]
    public async Task Stage_Returns409CampaignNotDraft_WhenTheCampaignIsLaunched()
    {
        var client = _factory.CreateClient();

        // Convoca el evento que sirve el vecino simulado: sin él el lanzamiento se niega en su
        // paso 4 y la campaña nunca llegaría a `sending`, que es la premisa de este test.
        var campaign = await CampaignTestClient.CreateAsync(client, EventsStub.EventId);

        (await CampaignTestClient.SelectAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var launched = await CampaignTestClient.LaunchAsync(client, campaign.Id, new { expectedRecipientCount = 1 });
        launched.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var response = await PostAsync(
            client,
            campaign.Id,
            UploadFiles.Csv(UploadFiles.Person("Tarde", "Llega", "tarde@acme.com")),
            "lista.csv",
            UploadFiles.CsvContentType);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeAsync(response)).ShouldBe("CAMPAIGN_NOT_DRAFT");
    }

    /// <summary>Confirmar sin nada pendiente lleva código propio: no es "esa campaña no existe".</summary>
    [Fact]
    public async Task Confirm_Returns404UploadNotFound_WhenNothingIsStaged()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        var response = await client.PostAsync(UploadRoute(campaign.Id) + "/confirm", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeAsync(response)).ShouldBe("UPLOAD_NOT_FOUND");
    }

    [Fact]
    public async Task Stage_Returns404_WhenTheCampaignDoesNotExist()
    {
        var client = _factory.CreateClient();

        var response = await PostAsync(
            client,
            Guid.NewGuid(),
            UploadFiles.Csv(UploadFiles.Person("Ana", "Pérez", "ana@acme.com")),
            "lista.csv",
            UploadFiles.CsvContentType);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Cancelar un borrador se lleva su carga sin confirmar, que es lo que sostiene que las
    /// cargas no necesiten ni caducidad ni tarea de limpieza (spec <c>05</c> §2).
    /// </summary>
    [Fact]
    public async Task Cancel_ReturnsNoPendingUploadLeft_WhenTheDraftIsCancelled()
    {
        var client = _factory.CreateClient();
        var campaign = await CampaignTestClient.CreateAsync(client, Guid.NewGuid());

        await StageAsync(client, campaign.Id, UploadFiles.Csv(
            UploadFiles.Person("Ana", "Pérez", "abandonada@acme.com")), "csv");

        (await CampaignTestClient.CancelAsync(client, campaign.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await CampaignTestClient.CountUploadsAsync(_factory, campaign.Id)).ShouldBe(0);
        (await CampaignTestClient.CountUploadRowsAsync(_factory, campaign.Id)).ShouldBe(0);
    }

    private static string UploadRoute(Guid campaignId) =>
        $"{CampaignTestClient.Route}/{campaignId}/recipient-uploads";

    private static MemoryStream Build(string format, params UploadFiles.Row[] rows) =>
        format == "xlsx" ? UploadFiles.Xlsx(rows) : UploadFiles.Csv(rows);

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        Guid campaignId,
        Stream content,
        string fileName,
        string contentType) =>
        client.PostAsync(UploadRoute(campaignId), UploadFiles.AsForm(content, fileName, contentType));

    private static async Task<RecipientUploadResponse> StageAsync(
        HttpClient client,
        Guid campaignId,
        Stream content,
        string format)
    {
        var response = await PostAsync(
            client,
            campaignId,
            content,
            format == "xlsx" ? "lista.xlsx" : "lista.csv",
            format == "xlsx" ? UploadFiles.XlsxContentType : UploadFiles.CsvContentType);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<RecipientUploadResponse>()
            ?? throw new InvalidOperationException("La revisión no devolvió resumen.");
    }

    private async Task<RecipientUploadResponse> ConfirmAsync(HttpClient client, Guid campaignId)
    {
        var response = await client.PostAsync(UploadRoute(campaignId) + "/confirm", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<RecipientUploadResponse>()
            ?? throw new InvalidOperationException("La confirmación no devolvió resumen.");
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        return problem.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static int Total(UploadCountsResponse counts) =>
        counts.Matched + counts.Unmatched + counts.Suppressed + counts.AlreadyARecipient
        + counts.DuplicateInFile + counts.Malformed + counts.CollapsedExactDuplicates;
}
