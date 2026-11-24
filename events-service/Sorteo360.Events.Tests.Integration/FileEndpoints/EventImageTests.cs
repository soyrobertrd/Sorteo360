using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Infrastructure.Database;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.Auth;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.GlobalEndpoints;
using Npgsql;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.FileEndpoints;

/// <summary>
/// El camino completo de una imagen de evento: subirla como portada o como foto de galería,
/// listarla, bajarla y quitarla, con el proveedor de sistema de ficheros de verdad detrás.
/// <para>
/// El cupo de galería es más bajo que el de producción a propósito: probarlo subiendo veinte
/// archivos mediría la tubería de prueba y no la regla.
/// </para>
/// </summary>
public sealed class EventImageTests : IClassFixture<EventImageTests.ConstrainedFactory>
{
    /// <summary>Un JPEG de verdad: es lo que llega como portada y como foto de galería.</summary>
    private static readonly byte[] ImageBytes = SampleFiles.Jpeg();

    private readonly ConstrainedFactory _factory;
    private readonly HttpClient _client;

    public EventImageTests(ConstrainedFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region POST /v1/events/{id}/images

    [Fact]
    public async Task Upload_Returns201WithBothIdsAndLocation_WhenTheEventExists()
    {
        var eventId = await CreateEventAsync();

        var response = await UploadAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EventImageResponse>();
        body.ShouldNotBeNull();
        body!.Id.ShouldNotBe(Guid.Empty);
        body.StoredFileId.ShouldNotBe(Guid.Empty);
        body.Id.ShouldNotBe(body.StoredFileId);
        body.FileName.ShouldBe("portada.jpg");
        body.ContentType.ShouldBe("image/jpeg");
        body.SizeBytes.ShouldBe(ImageBytes.Length);
        body.Kind.ShouldBe("Gallery");
        body.Missing.ShouldBeFalse();
        response.Headers.Location!.ToString().ShouldContain($"/v1/files/{body.StoredFileId}");
    }

    [Fact]
    public async Task Upload_KeepsTheCoverKind_WhenItIsAsked()
    {
        var eventId = await CreateEventAsync();

        var uploaded = await UploadedAsync(eventId, kind: "Cover");

        uploaded.Kind.ShouldBe("Cover");
        (await ListAsync(eventId)).Single().Kind.ShouldBe("Cover");
    }

    /// <summary>
    /// Omitir el <c>kind</c> archiva la imagen como galería y no como portada: la portada es
    /// única, así que caer en ella por omisión reemplazaría la que hubiera sin que nadie lo
    /// pidiera.
    /// </summary>
    [Fact]
    public async Task Upload_FilesItInTheGallery_WhenNoKindIsSent()
    {
        var eventId = await CreateEventAsync();

        (await UploadedAsync(eventId)).Kind.ShouldBe("Gallery");
    }

    /// <summary>
    /// Un <c>kind</c> vacío es un cliente que no eligió, que es el mismo caso que no mandarlo.
    /// </summary>
    [Fact]
    public async Task Upload_FilesItInTheGallery_WhenTheKindIsBlank()
    {
        var eventId = await CreateEventAsync();

        (await UploadedAsync(eventId, kind: "")).Kind.ShouldBe("Gallery");
    }

    [Fact]
    public async Task Upload_Returns400WithTheKindCode_WhenTheKindIsNotOneThatExists()
    {
        var eventId = await CreateEventAsync();

        var response = await UploadAsync(eventId, kind: "Banner");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("EVENT_IMAGE_KIND_NOT_SUPPORTED");
    }

    /// <summary>
    /// Distingue mayúsculas, igual que la moneda: un valor casi correcto se rechaza en vez de
    /// adivinarse, porque adivinar es lo que reemplazaría una portada sin que nadie lo pidiera.
    /// </summary>
    [Fact]
    public async Task Upload_Returns400WithTheKindCode_WhenTheKindDiffersOnlyInCasing()
    {
        var eventId = await CreateEventAsync();

        var response = await UploadAsync(eventId, kind: "cover");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("EVENT_IMAGE_KIND_NOT_SUPPORTED");
    }

    [Fact]
    public async Task Upload_WritesNothing_WhenTheKindIsNotOneThatExists()
    {
        var eventId = await CreateEventAsync();

        await UploadAsync(eventId, kind: "Banner");

        (await ListAsync(eventId)).ShouldBeEmpty();
    }

    /// <summary>
    /// Los tres formatos que se muestran entran por la ruta de verdad, no solo por la regla: lo
    /// que se guarda y se vuelve a servir es el tipo que dicen los bytes.
    /// </summary>
    [Fact]
    public async Task Upload_AcceptsItAndServesItBackAsAPng_WhenTheFileIsAPng()
    {
        var eventId = await CreateEventAsync();

        var uploaded = await UploadedAsync(eventId, SampleFiles.Png(), "portada.png", "image/png");

        uploaded.ContentType.ShouldBe("image/png");

        var content = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");
        content.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
    }

    [Fact]
    public async Task Upload_Returns404WithTheEventCode_WhenTheEventDoesNotExist()
    {
        var response = await UploadAsync(Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task Upload_Returns400_WhenNoFileIsSent()
    {
        var eventId = await CreateEventAsync();

        using var content = new MultipartFormDataContent
        {
            { new StringContent("sin archivo"), "comment" }
        };

        var response = await _client.PostAsync($"v1/events/{eventId}/images", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_Returns400_WhenTheFileIsEmpty()
    {
        var eventId = await CreateEventAsync();

        var response = await UploadAsync(eventId, bytes: []);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_Returns403_WhenTheCallerCannotWriteEvents()
    {
        var eventId = await CreateEventAsync();

        using var request = BuildUploadRequest(eventId, ImageBytes, "portada.jpg", "image/jpeg");
        request.Headers.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.RafflesWrite);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Las reglas de contenido ya existen y se aplican acá igual: lo que decide el tipo son los
    /// bytes, no lo que declare quien sube el archivo.
    /// </summary>
    [Fact]
    public async Task Upload_Returns400WithTypeNotAllowed_WhenTheContentIsNotAnImageAtAll()
    {
        var eventId = await CreateEventAsync();

        var response = await UploadAsync(eventId, SampleFiles.Html(), "portada.jpg", "image/jpeg");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("FILE_TYPE_NOT_ALLOWED");
    }

    /// <summary>
    /// Un PDF pasa las reglas generales de la plataforma y aun así no entra acá: lo que se sube a
    /// un evento se muestra en una página, y un PDF no se puede enseñar.
    /// </summary>
    [Fact]
    public async Task Upload_Returns400WithTypeNotAllowed_WhenTheContentIsAPdf()
    {
        var eventId = await CreateEventAsync();

        var response = await UploadAsync(eventId, SampleFiles.Pdf(), "portada.pdf", "application/pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("FILE_TYPE_NOT_ALLOWED");
        (await ListAsync(eventId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_Returns400WithTheHeicCode_WhenTheContentIsAnIphonePhoto()
    {
        var eventId = await CreateEventAsync();

        var response = await UploadAsync(eventId, SampleFiles.Heic(), "portada.heic", "image/heic");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("FILE_TYPE_HEIC_NOT_SUPPORTED");
    }

    #endregion

    #region The cover is one per event

    /// <summary>
    /// Subir una segunda portada reemplaza a la primera en vez de rechazarse: cambiar la portada
    /// es un clic, y exigir que primero se quite la vieja dejaría al evento sin ninguna si la
    /// segunda llamada falla.
    /// </summary>
    [Fact]
    public async Task Upload_ReplacesTheCover_WhenASecondOneIsUploaded()
    {
        var eventId = await CreateEventAsync();
        var first = await UploadedAsync(eventId, ImageBytes, "vieja.jpg", kind: "Cover");

        var second = await UploadedAsync(eventId, ImageBytes, "nueva.jpg", kind: "Cover");

        var images = await ListAsync(eventId);
        var cover = images.ShouldHaveSingleItem();
        cover.Id.ShouldBe(second.Id);
        cover.FileName.ShouldBe("nueva.jpg");
        cover.Id.ShouldNotBe(first.Id);
    }

    /// <summary>
    /// La portada reemplazada se desenlaza, no se borra: sus bytes siguen respondiendo por su id,
    /// igual que si se la hubiera quitado a mano.
    /// </summary>
    [Fact]
    public async Task Upload_KeepsTheReplacedCoverBytes_WhenTheCoverChanges()
    {
        var eventId = await CreateEventAsync();
        var first = await UploadedAsync(eventId, ImageBytes, "vieja.jpg", kind: "Cover");

        await UploadedAsync(eventId, ImageBytes, "nueva.jpg", kind: "Cover");

        var content = await _client.GetAsync($"v1/files/{first.StoredFileId}/content");
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await content.Content.ReadAsByteArrayAsync()).ShouldBe(ImageBytes);
    }

    /// <summary>
    /// Reemplazar la portada no toca la galería: son dos cosas distintas y el cupo de una no es
    /// el de la otra.
    /// </summary>
    [Fact]
    public async Task Upload_LeavesTheGalleryAlone_WhenTheCoverIsReplaced()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId, ImageBytes, "galeria.jpg");
        await UploadedAsync(eventId, ImageBytes, "vieja.jpg", kind: "Cover");

        await UploadedAsync(eventId, ImageBytes, "nueva.jpg", kind: "Cover");

        var images = await ListAsync(eventId);
        images.Select(i => i.FileName).ShouldBe(["nueva.jpg", "galeria.jpg"]);
    }

    /// <summary>
    /// La regla vive en la base y no solo en el servicio: dos subidas simultáneas pasarían las
    /// dos por la comprobación, y lo que las serializa es el índice único parcial. Se escribe la
    /// segunda portada por detrás del servicio para comprobar que la base la rechaza.
    /// </summary>
    [Fact]
    public async Task TheDatabase_RefusesASecondLiveCover_WhenOneIsWrittenBehindTheService()
    {
        var eventId = await CreateEventAsync();
        var first = await UploadedAsync(eventId, ImageBytes, "portada.jpg", kind: "Cover");

        var conflict = await Should.ThrowAsync<PostgresException>(
            () => InsertCoverDirectlyAsync(eventId, first.StoredFileId));

        conflict.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        conflict.ConstraintName.ShouldBe("ux_event_image_cover_per_event");
    }

    /// <summary>
    /// Y una portada dada de baja no ocupa lugar: el índice solo cuenta las vivas, que es lo que
    /// deja reemplazar la portada tantas veces como haga falta.
    /// </summary>
    [Fact]
    public async Task TheDatabase_AcceptsANewCover_WhenTheOldOneIsAlreadyUnlinked()
    {
        var eventId = await CreateEventAsync();
        var first = await UploadedAsync(eventId, ImageBytes, "vieja.jpg", kind: "Cover");
        await _client.DeleteAsync($"v1/events/{eventId}/images/{first.Id}");

        await InsertCoverDirectlyAsync(eventId, first.StoredFileId);

        (await ListAsync(eventId)).ShouldHaveSingleItem().Kind.ShouldBe("Cover");
    }

    /// <summary>
    /// La portada encabeza la lista, venga en el orden que venga: quien la muestra la toma de la
    /// primera posición sin recorrer nada.
    /// </summary>
    [Fact]
    public async Task List_PutsTheCoverFirst_WhenUploadedInAnyOrder()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId, ImageBytes, "primera.jpg");
        await UploadedAsync(eventId, ImageBytes, "segunda.jpg");
        await UploadedAsync(eventId, ImageBytes, "portada.jpg", kind: "Cover");

        var images = await ListAsync(eventId);

        images.Select(i => i.FileName).ShouldBe(["portada.jpg", "primera.jpg", "segunda.jpg"]);
    }

    #endregion

    #region Quotas

    [Fact]
    public async Task Upload_Returns409WithTheLimitCode_WhenTheGalleryIsAtItsQuota()
    {
        var eventId = await CreateEventAsync();

        for (var i = 0; i < ConstrainedFactory.MaxGalleryImages; i++)
            await UploadedAsync(eventId, ImageBytes, $"galeria-{i}.jpg");

        var response = await UploadAsync(eventId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("EVENT_GALLERY_IMAGE_LIMIT_REACHED");
    }

    /// <summary>
    /// El cupo es solo de la galería. La portada tiene su propia regla —una— y sumarla acá
    /// dejaría la galería en una foto menos de las que dice el número.
    /// </summary>
    [Fact]
    public async Task Upload_AcceptsTheCover_WhenTheGalleryIsAlreadyFull()
    {
        var eventId = await CreateEventAsync();

        for (var i = 0; i < ConstrainedFactory.MaxGalleryImages; i++)
            await UploadedAsync(eventId, ImageBytes, $"galeria-{i}.jpg");

        var response = await UploadAsync(eventId, kind: "Cover");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>
    /// Quitar una foto libera su lugar: el cupo cuenta las vivas, no las que alguna vez hubo.
    /// </summary>
    [Fact]
    public async Task Upload_AcceptsOneMore_WhenAGalleryImageIsRemoved()
    {
        var eventId = await CreateEventAsync();
        var uploaded = new List<EventImageResponse>();

        for (var i = 0; i < ConstrainedFactory.MaxGalleryImages; i++)
            uploaded.Add(await UploadedAsync(eventId, ImageBytes, $"galeria-{i}.jpg"));

        await _client.DeleteAsync($"v1/events/{eventId}/images/{uploaded[0].Id}");

        (await UploadAsync(eventId)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    #endregion

    #region GET /v1/events/{id}/images

    [Fact]
    public async Task List_ReturnsWhatWasUploadedWithBothIds_WhenTheEventHasImages()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedAsync(eventId);

        var image = (await ListAsync(eventId)).ShouldHaveSingleItem();

        image.Id.ShouldBe(uploaded.Id);
        image.StoredFileId.ShouldBe(uploaded.StoredFileId);
        image.FileName.ShouldBe("portada.jpg");
        image.ContentType.ShouldBe("image/jpeg");
        image.SizeBytes.ShouldBe(ImageBytes.Length);
        image.Missing.ShouldBeFalse();
    }

    [Fact]
    public async Task List_ReturnsBothKinds_WhenNoFilterIsGiven()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId, ImageBytes, "portada.jpg", kind: "Cover");
        await UploadedAsync(eventId, ImageBytes, "galeria.jpg");

        (await ListAsync(eventId)).Select(i => i.Kind).ShouldBe(["Cover", "Gallery"]);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheKindAskedFor_WhenItIsFiltered()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId, ImageBytes, "portada.jpg", kind: "Cover");
        await UploadedAsync(eventId, ImageBytes, "galeria.jpg");

        (await ListAsync(eventId, kind: "Cover")).Single().FileName.ShouldBe("portada.jpg");
        (await ListAsync(eventId, kind: "Gallery")).Single().FileName.ShouldBe("galeria.jpg");
    }

    [Fact]
    public async Task List_ReturnsAnEmptyList_WhenTheEventHasNoneOfThatKind()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId);

        (await ListAsync(eventId, kind: "Cover")).ShouldBeEmpty();
    }

    [Fact]
    public async Task List_ReturnsBothKinds_WhenTheFilterIsBlank()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId, ImageBytes, "portada.jpg", kind: "Cover");
        await UploadedAsync(eventId, ImageBytes, "galeria.jpg");

        var response = await ListResponseAsync(eventId, kind: "");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<EventImageResponse>>())!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task List_Returns400WithTheKindCode_WhenTheFilterIsNotOneThatExists()
    {
        var eventId = await CreateEventAsync();

        var response = await ListResponseAsync(eventId, kind: "Banner");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("EVENT_IMAGE_KIND_NOT_SUPPORTED");
    }

    /// <summary>
    /// Una fila escrita sin <c>kind</c> es la forma que tendría una imagen anterior a la columna,
    /// y sale como foto de galería. El valor por omisión es lo que hace que ese insert pase.
    /// </summary>
    [Fact]
    public async Task List_ReadsTheRowAsAGalleryImage_WhenItWasWrittenWithoutAKind()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedAsync(eventId);

        await InsertImageWithoutKindAsync(eventId, uploaded.StoredFileId);

        var images = await ListAsync(eventId);

        images.Count.ShouldBe(2);
        images.Select(i => i.Kind).ShouldAllBe(kind => kind == "Gallery");
    }

    [Fact]
    public async Task List_ReturnsAnEmptyList_WhenTheEventHasNoImages()
    {
        var eventId = await CreateEventAsync();

        (await ListAsync(eventId)).ShouldBeEmpty();
    }

    /// <summary>
    /// Un evento que no existe y una imagen que no existe se responden desde la misma ruta, y
    /// llevan códigos distintos para poder distinguirlos.
    /// </summary>
    [Fact]
    public async Task List_Returns404WithTheEventCode_WhenTheEventDoesNotExist()
    {
        var response = await _client.GetAsync($"v1/events/{Guid.NewGuid()}/images");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task List_Returns401_WhenTheCallerIsAnonymous()
    {
        var eventId = await CreateEventAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/events/{eventId}/images");
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region The images follow their Event

    [Fact]
    public async Task GetEventById_ReturnsTheImagesWithTheCoverFirst_WhenTheEventHasImages()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId, ImageBytes, "galeria.jpg");
        var cover = await UploadedAsync(eventId, ImageBytes, "portada.jpg", kind: "Cover");

        var detail = await _client.GetFromJsonAsync<EventDetailResponse>($"v1/events/{eventId}");

        detail!.Images.Select(i => i.FileName).ShouldBe(["portada.jpg", "galeria.jpg"]);
        detail.Images[0].Id.ShouldBe(cover.Id);
        detail.Images[0].StoredFileId.ShouldBe(cover.StoredFileId);
        detail.Images[0].Kind.ShouldBe("Cover");
    }

    [Fact]
    public async Task GetEventById_ReturnsAnEmptyImageList_WhenTheEventHasNoImages()
    {
        var eventId = await CreateEventAsync();

        var detail = await _client.GetFromJsonAsync<EventDetailResponse>($"v1/events/{eventId}");

        detail!.Images.ShouldBeEmpty();
    }

    /// <summary>
    /// Una imagen quitada deja de acompañar al evento dondequiera que se lea, no solo en su
    /// propia lista.
    /// </summary>
    [Fact]
    public async Task GetEventById_DropsTheImage_WhenItIsRemoved()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedAsync(eventId);

        var removed = await _client.DeleteAsync($"v1/events/{eventId}/images/{uploaded.Id}");
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await _client.GetFromJsonAsync<EventDetailResponse>($"v1/events/{eventId}");

        detail!.Images.ShouldBeEmpty();
    }

    /// <summary>
    /// Editar el evento devuelve la misma ficha que leerlo, imágenes incluidas: si el PATCH las
    /// devolviera vacías, el formulario creería que se perdieron al guardar.
    /// </summary>
    [Fact]
    public async Task UpdateEvent_ReturnsTheImagesLikeTheDetailRead_WhenTheEventHasImages()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId, ImageBytes, "portada.jpg", kind: "Cover");

        var response = await _client.PatchAsJsonAsync($"v1/events/{eventId}", new UpdateEventRequest
        {
            Name        = "Roadshow Imagenes (editado)",
            StartDate   = new DateOnly(2026, 9, 1),
            EndDate     = new DateOnly(2026, 9, 5),
            Language    = AllowedLanguage.Espanol
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = (await response.Content.ReadFromJsonAsync<EventDetailResponse>())!;
        detail.Images.ShouldHaveSingleItem().FileName.ShouldBe("portada.jpg");
    }

    #endregion

    #region The cover reaches the list projection

    /// <summary>
    /// La portada llega al listado y no solo al detalle: la página pública se arma con la fila
    /// del listado, y sin esto habría que pedir el detalle de cada evento para pintar una grilla.
    /// </summary>
    [Fact]
    public async Task ListEvents_ReturnsTheCover_WhenTheEventHasOne()
    {
        var eventId = await CreateEventAsync();
        var cover = await UploadedAsync(eventId, ImageBytes, "portada.jpg", kind: "Cover");

        var row = await ListedEventAsync(eventId);

        row.Cover.ShouldNotBeNull();
        row.Cover!.Id.ShouldBe(cover.Id);
        row.Cover.StoredFileId.ShouldBe(cover.StoredFileId);
        row.Cover.FileName.ShouldBe("portada.jpg");
        row.Cover.ContentType.ShouldBe("image/jpeg");
        row.Cover.Kind.ShouldBe("Cover");
    }

    [Fact]
    public async Task ListEvents_ReturnsANullCover_WhenTheEventHasNone()
    {
        var eventId = await CreateEventAsync();

        (await ListedEventAsync(eventId)).Cover.ShouldBeNull();
    }

    /// <summary>
    /// El listado trae la portada y nada más. La galería es una lectura del detalle: una página
    /// de veinticinco eventos no tiene por qué arrastrar todas las fotos de cada uno.
    /// </summary>
    [Fact]
    public async Task ListEvents_IgnoresTheGallery_WhenTheEventHasNoCover()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId, ImageBytes, "galeria.jpg");

        (await ListedEventAsync(eventId)).Cover.ShouldBeNull();
    }

    [Fact]
    public async Task ListEvents_ReturnsTheNewCover_WhenTheCoverIsReplaced()
    {
        var eventId = await CreateEventAsync();
        await UploadedAsync(eventId, ImageBytes, "vieja.jpg", kind: "Cover");
        var second = await UploadedAsync(eventId, ImageBytes, "nueva.jpg", kind: "Cover");

        var row = await ListedEventAsync(eventId);

        row.Cover!.Id.ShouldBe(second.Id);
        row.Cover.FileName.ShouldBe("nueva.jpg");
    }

    [Fact]
    public async Task ListEvents_ReturnsANullCover_WhenTheCoverIsRemoved()
    {
        var eventId = await CreateEventAsync();
        var cover = await UploadedAsync(eventId, ImageBytes, "portada.jpg", kind: "Cover");

        await _client.DeleteAsync($"v1/events/{eventId}/images/{cover.Id}");

        (await ListedEventAsync(eventId)).Cover.ShouldBeNull();
    }

    #endregion

    #region GET /v1/files/{id}

    [Fact]
    public async Task GetFileContent_ReturnsExactlyWhatWasUploaded_WhenTheImageExists()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedAsync(eventId);

        var response = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(ImageBytes);
    }

    #endregion

    #region DELETE /v1/events/{id}/images/{imageId}

    [Fact]
    public async Task Remove_Returns204AndDropsItFromTheList_WhenTheImageExists()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedAsync(eventId);

        var response = await _client.DeleteAsync($"v1/events/{eventId}/images/{uploaded.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(eventId)).ShouldBeEmpty();
    }

    /// <summary>
    /// Quitar la imagen desenlaza y no borra: el archivo sigue respondiendo por su id, que es lo
    /// que deja la purga de retención como una decisión posterior y no como un rescate.
    /// </summary>
    [Fact]
    public async Task Remove_KeepsTheFileAndItsBytes_WhenTheImageIsRemoved()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedAsync(eventId);

        await _client.DeleteAsync($"v1/events/{eventId}/images/{uploaded.Id}");

        var metadata = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}");
        metadata.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");
        content.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await content.Content.ReadAsByteArrayAsync()).ShouldBe(ImageBytes);
    }

    /// <summary>
    /// Quitada la portada, el evento se queda sin ninguna hasta que se suba otra, y la siguiente
    /// entra sin chocar con la que se fue.
    /// </summary>
    [Fact]
    public async Task Remove_LetsANewCoverIn_WhenTheOldOneIsGone()
    {
        var eventId = await CreateEventAsync();
        var cover = await UploadedAsync(eventId, ImageBytes, "vieja.jpg", kind: "Cover");

        await _client.DeleteAsync($"v1/events/{eventId}/images/{cover.Id}");
        (await ListAsync(eventId)).ShouldBeEmpty();

        var replacement = await UploadedAsync(eventId, ImageBytes, "nueva.jpg", kind: "Cover");

        (await ListAsync(eventId)).ShouldHaveSingleItem().Id.ShouldBe(replacement.Id);
    }

    [Fact]
    public async Task Remove_Returns404WithTheImageCode_WhenItIsNotThere()
    {
        var eventId = await CreateEventAsync();

        var response = await _client.DeleteAsync($"v1/events/{eventId}/images/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("EVENT_IMAGE_NOT_FOUND");
    }

    /// <summary>
    /// El evento es parte del filtro y no solo de la ruta: sin eso, cualquiera con un id de
    /// imagen podría desenlazarla desde otro evento.
    /// </summary>
    [Fact]
    public async Task Remove_Returns404_WhenTheImageBelongsToAnotherEvent()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedAsync(eventId);
        var otherEventId = await CreateEventAsync();

        var response = await _client.DeleteAsync($"v1/events/{otherEventId}/images/{uploaded.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListAsync(eventId)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Remove_Returns403_WhenTheCallerCannotWriteEvents()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedAsync(eventId);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"v1/events/{eventId}/images/{uploaded.Id}");
        request.Headers.Add(TestAuthHandler.RolesHeader, AuthorizationPolicies.RafflesWrite);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Escribe una portada directamente en la tabla, saltándose el servicio. Es la única forma de
    /// llegar al índice único: por la API, el reemplazo da de baja la anterior antes de escribir.
    /// </summary>
    private async Task InsertCoverDirectlyAsync(Guid eventId, Guid storedFileId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO events.event_image
                (id, event_id, stored_file_id, kind, created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                (@id, @eventId, @storedFileId, 'Cover', now(), now(), @actor, @actor)
            """,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("eventId", eventId),
            new NpgsqlParameter("storedFileId", storedFileId),
            new NpgsqlParameter("actor", Guid.NewGuid()));
    }

    /// <summary>
    /// Escribe un enlace directamente en la tabla, sin la columna <c>kind</c>. Reusa un
    /// <c>stored_file</c> ya subido porque lo que se está probando es el enlace y no los bytes.
    /// </summary>
    private async Task InsertImageWithoutKindAsync(Guid eventId, Guid storedFileId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO events.event_image
                (id, event_id, stored_file_id, created_at, updated_at, created_by_id, updated_by_id)
            VALUES
                (@id, @eventId, @storedFileId, now(), now(), @actor, @actor)
            """,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("eventId", eventId),
            new NpgsqlParameter("storedFileId", storedFileId),
            new NpgsqlParameter("actor", Guid.NewGuid()));
    }

    private async Task<Guid> CreateEventAsync()
    {
        var response = await _client.PostAsJsonAsync("v1/events", new CreateEventRequest
        {
            Type        = EventType.Corporate,
            Name        = $"Roadshow Imagenes {Guid.NewGuid():N}",
            StartDate   = new DateOnly(2026, 9, 1),
            EndDate     = new DateOnly(2026, 9, 5),
            City        = "Santo Domingo",
            Country     = "Dominican Republic"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>La fila de ese evento en el listado, buscada por su id para no depender del orden.</summary>
    private async Task<EventListItemResponse> ListedEventAsync(Guid eventId)
    {
        var response = await _client.GetAsync("v1/events?pageSize=100");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<EventListItemResponse>>())!;
        return page.Items.Single(e => e.Id == eventId);
    }

    private async Task<List<EventImageResponse>> ListAsync(Guid eventId, string? kind = null)
    {
        var response = await ListResponseAsync(eventId, kind);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<EventImageResponse>>())!;
    }

    private Task<HttpResponseMessage> ListResponseAsync(Guid eventId, string? kind = null)
    {
        var route = $"v1/events/{eventId}/images";
        return _client.GetAsync(kind is null ? route : $"{route}?kind={kind}");
    }

    private async Task<EventImageResponse> UploadedAsync(
        Guid eventId,
        byte[]? bytes = null,
        string fileName = "portada.jpg",
        string contentType = "image/jpeg",
        string? kind = null)
    {
        var response = await UploadAsync(eventId, bytes, fileName, contentType, kind);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EventImageResponse>())!;
    }

    private Task<HttpResponseMessage> UploadAsync(
        Guid eventId,
        byte[]? bytes = null,
        string fileName = "portada.jpg",
        string contentType = "image/jpeg",
        string? kind = null)
    {
        return _client.SendAsync(BuildUploadRequest(eventId, bytes ?? ImageBytes, fileName, contentType, kind));
    }

    private static HttpRequestMessage BuildUploadRequest(
        Guid eventId,
        byte[] bytes,
        string fileName,
        string contentType,
        string? kind = null)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var content = new MultipartFormDataContent { { file, "file", fileName } };

        if (kind is not null)
            content.Add(new StringContent(kind), "kind");

        return new HttpRequestMessage(HttpMethod.Post, $"v1/events/{eventId}/images")
        {
            Content = content
        };
    }

    #endregion

    /// <summary>
    /// La misma aplicación con el cupo de galería bajado, para poder agotarlo sin subir veinte
    /// archivos por el solo gusto de contarlos.
    /// </summary>
    public sealed class ConstrainedFactory : EventsApiDbFactory
    {
        public const int MaxGalleryImages = 2;

        protected override int MaxGalleryImagesPerEvent => MaxGalleryImages;
    }
}
