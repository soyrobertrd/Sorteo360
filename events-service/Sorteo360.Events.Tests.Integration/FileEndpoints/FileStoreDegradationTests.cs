using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sorteo360.Events.Core.Events;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.FileEndpoints;
using Sorteo360.Events.Web.GlobalEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.FileEndpoints;

/// <summary>
/// Las dos formas en que el almacenamiento deja de entregar un archivo, que desde el punto de
/// llamada se parecen y nunca significan lo mismo: contestar que no está es una pérdida, y no
/// contestar es una avería. Todo lo que sigue existe para que un reinicio de cinco minutos no
/// pueda dar por perdida la biblioteca entera.
/// </summary>
public sealed class FileStoreDegradationTests : IClassFixture<FileStoreOutageApiDbFactory>
{
    private static readonly byte[] PhotoBytes = SampleFiles.Jpeg();

    private readonly FileStoreOutageApiDbFactory _factory;
    private readonly HttpClient _client;

    public FileStoreDegradationTests(FileStoreOutageApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region El almacenamiento no contesta

    [Fact]
    public async Task GetFileContent_Returns503WithItsOwnCode_WhenTheStoreIsUnreachable()
    {
        var uploaded = await UploadedPhotoAsync();

        using (_factory.Failing(FileStoreFault.Unavailable))
        {
            var response = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadAsStringAsync()).ShouldContain("FILE_STORE_UNAVAILABLE");
        }
    }

    [Fact]
    public async Task GetFileContent_Returns503WithItsOwnCode_WhenTheStoreTimesOut()
    {
        var uploaded = await UploadedPhotoAsync();

        using (_factory.Failing(FileStoreFault.Timeout))
        {
            var response = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadAsStringAsync()).ShouldContain("FILE_STORE_UNAVAILABLE");
        }
    }

    /// <summary>
    /// Subir también depende del almacenamiento. Falla con el mismo código transitorio, y sobre
    /// todo no deja fila: lo que no se pudo guardar no existe, ni siquiera como archivo perdido.
    /// </summary>
    [Fact]
    public async Task UploadImage_Returns503AndCreatesNothing_WhenTheStoreIsUnreachable()
    {
        var eventId = await CreateEventAsync();
        var before = await MissingFilesAsync();

        using (_factory.Failing(FileStoreFault.Unavailable))
        {
            var file = new ByteArrayContent(PhotoBytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            var content = new MultipartFormDataContent { { file, "file", "salon.jpg" } };

            var response = await _client.PostAsync($"v1/events/{eventId}/images", content);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadAsStringAsync()).ShouldContain("FILE_STORE_UNAVAILABLE");
        }

        var response2 = await _client.GetAsync($"v1/events/{eventId}/images");
        (await response2.Content.ReadFromJsonAsync<List<EventImageResponse>>())!.ShouldBeEmpty();
        (await MissingFilesAsync()).Total.ShouldBe(before.Total);
    }

    /// <summary>
    /// El caso que todo esto existe para evitar. El almacenamiento se cae, se lo golpea desde
    /// varios lados, y al volver ninguna ficha quedó marcada: el registro de qué se perdió de
    /// verdad sigue intacto.
    /// </summary>
    [Fact]
    public async Task GetFileContent_MarksNothingAsMissingHoweverManyReadsFail_WhenTheStoreIsUnreachable()
    {
        var uploaded = await UploadedPhotoAsync();

        using (_factory.Failing(FileStoreFault.Unavailable))
        {
            await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");
            await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");
        }

        using (_factory.Failing(FileStoreFault.Timeout))
        {
            await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");
        }

        (await FileAsync(uploaded.StoredFileId)).Missing.ShouldBeFalse();
        (await MissingFilesAsync()).Items.ShouldNotContain(f => f.Id == uploaded.StoredFileId);

        // Y al volver el almacenamiento el archivo se baja igual que antes: la avería no dejó
        // nada escrito que haya que deshacer.
        var afterwards = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");
        afterwards.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region El almacenamiento contesta que no está

    /// <summary>
    /// Se borran los bytes por fuera. La primera descarga que se topa con el hueco es la que lo
    /// deja anotado, y a partir de ahí la ficha lo dice sin volver a preguntarle al
    /// almacenamiento.
    /// </summary>
    [Fact]
    public async Task GetFileContent_RecordsTheFileAsMissing_WhenTheStoreSaysItIsGone()
    {
        var uploaded = await UploadedPhotoAsync();
        (await FileAsync(uploaded.StoredFileId)).Missing.ShouldBeFalse();

        DeleteFromTheStore(uploaded);
        var download = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

        download.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await download.Content.ReadAsStringAsync()).ShouldContain("FILE_MISSING");
        (await FileAsync(uploaded.StoredFileId)).Missing.ShouldBeTrue();
    }

    /// <summary>
    /// Un archivo perdido no se esconde. La galería del evento lo sigue listando con su nombre
    /// y su fecha, marcado, porque eso es lo que dice qué hay que restaurar; una pantalla pinta
    /// un hueco donde había una foto, no una página rota.
    /// </summary>
    [Fact]
    public async Task ListImages_KeepsReturningTheImageFlaggedWithItsOriginalMetadata_WhenItIsMissing()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedPhotoAsync(eventId);
        var before = await SingleImageAsync(eventId);

        DeleteFromTheStore(uploaded);
        await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

        var response = await _client.GetAsync($"v1/events/{eventId}/images");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = (await response.Content.ReadFromJsonAsync<List<EventImageResponse>>())!
            .ShouldHaveSingleItem();

        after.Missing.ShouldBeTrue();
        after.FileName.ShouldBe(before.FileName);
        after.CreatedAt.ShouldBe(before.CreatedAt);
        after.SizeBytes.ShouldBe(before.SizeBytes);
    }

    /// <summary>
    /// Lo que necesita quien va a restaurarlo desde la papelera del almacenamiento: cómo se
    /// llamaba, quién lo subió, cuándo, y desde cuándo falta.
    /// </summary>
    [Fact]
    public async Task ListMissingFiles_ShowsWhatAnOperatorNeedsToRestoreIt_WhenAFileIsMissing()
    {
        var uploaded = await UploadedPhotoAsync();
        DeleteFromTheStore(uploaded);
        await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

        var page = await MissingFilesAsync();
        page.Items.ShouldContain(f => f.Id == uploaded.StoredFileId);
        var missing = page.Items.Single(f => f.Id == uploaded.StoredFileId);

        missing.FileName.ShouldBe("salon.jpg");
        missing.ContentType.ShouldBe("image/jpeg");
        missing.SizeBytes.ShouldBe(PhotoBytes.Length);
        missing.CreatedById.ShouldBe(TestAuthHandler.DefaultUserId);
        missing.CreatedAt.ShouldNotBe(default);
        missing.MissingSince.ShouldNotBe(default);
    }

    /// <summary>
    /// La fecha que importa es la primera: cada descarga posterior vuelve a pasar por el mismo
    /// camino, y mover el sello borraría desde cuándo falta de verdad.
    /// </summary>
    [Fact]
    public async Task MissingSince_KeepsTheFirstMoment_WhenLaterReadsFindItMissingToo()
    {
        var uploaded = await UploadedPhotoAsync();
        DeleteFromTheStore(uploaded);

        await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");
        var first = await MissingSinceAsync(uploaded.StoredFileId);

        await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");
        var second = await MissingSinceAsync(uploaded.StoredFileId);

        second.ShouldBe(first);
    }

    /// <summary>
    /// Restaurado desde la papelera, el archivo vuelve a bajarse y sale del listado: si la marca
    /// no se borrara, la lista del operador se llenaría de archivos que ya no faltan.
    /// </summary>
    [Fact]
    public async Task GetFileContent_DownloadsAgainAndLeavesTheMissingList_WhenTheFileIsRestored()
    {
        var uploaded = await UploadedPhotoAsync();
        DeleteFromTheStore(uploaded);
        await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");
        (await FileAsync(uploaded.StoredFileId)).Missing.ShouldBeTrue();

        RestoreToTheStore(uploaded);
        var download = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await FileAsync(uploaded.StoredFileId)).Missing.ShouldBeFalse();
        (await MissingFilesAsync()).Items.ShouldNotContain(f => f.Id == uploaded.StoredFileId);
    }

    #endregion

    #region GET /v1/files/missing

    [Fact]
    public async Task ListMissingFiles_Returns401_WhenTheRequestIsAnonymous()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "v1/files/missing");
        request.Headers.Add(TestAuthHandler.AnonymousHeader, "true");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task ListMissingFiles_Returns400_WhenThePagingIsOutOfRange(string query)
    {
        var response = await _client.GetAsync($"v1/files/missing?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helpers

    private async Task<FileResponse> FileAsync(Guid storedFileId)
    {
        var response = await _client.GetAsync($"v1/files/{storedFileId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<FileResponse>())!;
    }

    private async Task<DateTimeOffset> MissingSinceAsync(Guid storedFileId)
    {
        var page = await MissingFilesAsync();
        page.Items.ShouldContain(f => f.Id == storedFileId);
        return page.Items.Single(f => f.Id == storedFileId).MissingSince;
    }

    private async Task<PagedResponse<MissingFileResponse>> MissingFilesAsync()
    {
        var response = await _client.GetAsync("v1/files/missing?pageSize=100");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PagedResponse<MissingFileResponse>>())!;
    }

    private async Task<EventImageResponse> SingleImageAsync(Guid eventId)
    {
        var response = await _client.GetAsync($"v1/events/{eventId}/images");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<EventImageResponse>>())!
            .ShouldHaveSingleItem();
    }

    /// <summary>
    /// Sube una imagen y devuelve, además de la respuesta, la ruta donde el almacenamiento dejó los
    /// bytes: es la que un test borra o restaura, y así nunca toca los archivos de otro.
    /// </summary>
    private async Task<UploadedPhoto> UploadedPhotoAsync(Guid? ownerEventId = null)
    {
        var owner = ownerEventId ?? await CreateEventAsync();
        var before = StoredFilePaths();

        var file = new ByteArrayContent(PhotoBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var content = new MultipartFormDataContent { { file, "file", "salon.jpg" } };

        var response = await _client.PostAsync($"v1/events/{owner}/images", content);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<EventImageResponse>())!;

        var path = StoredFilePaths().Except(before).ShouldHaveSingleItem();
        return new UploadedPhoto(body.StoredFileId, path);
    }

    /// <summary>Una imagen subida, con el id de su ficha y la ruta de sus bytes en el disco.</summary>
    private sealed record UploadedPhoto(Guid StoredFileId, string Path);

    private async Task<Guid> CreateEventAsync()
    {
        var response = await _client.PostAsJsonAsync("v1/events", new CreateEventRequest
        {
            Type      = EventType.Corporate,
            Name      = $"Evento Imagen {Guid.NewGuid():N}",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate   = new DateOnly(2026, 9, 5),
            City      = "Santo Domingo",
            Country   = "Dominican Republic",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Borra los bytes por detrás del servicio, como haría alguien limpiando una carpeta o un
    /// antivirus poniendo un archivo en cuarentena.
    /// </summary>
    private static void DeleteFromTheStore(UploadedPhoto photo) => File.Delete(photo.Path);

    /// <summary>Devuelve el archivo a su sitio, como haría quien lo saca de la papelera.</summary>
    private static void RestoreToTheStore(UploadedPhoto photo) =>
        File.WriteAllBytes(photo.Path, PhotoBytes);

    /// <summary>
    /// Los archivos que hay en el disco ahora mismo. La carpeta raíz la crea el proveedor al
    /// guardar el primero, así que antes de eso no existe y la respuesta correcta es "ninguno".
    /// </summary>
    private string[] StoredFilePaths() =>
        Directory.Exists(_factory.FileStoreRootPath)
            ? Directory.GetFiles(_factory.FileStoreRootPath, "*", SearchOption.AllDirectories)
            : [];

    #endregion
}
