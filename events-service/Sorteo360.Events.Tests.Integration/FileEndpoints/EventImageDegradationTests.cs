using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sorteo360.Events.Tests.Integration.Infrastructure;
using Sorteo360.Events.Web.EventEndpoints;
using Sorteo360.Events.Web.FileEndpoints;
using Sorteo360.Events.Web.GlobalEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Integration.FileEndpoints;

/// <summary>
/// Las dos formas en que el almacenamiento deja de entregar la imagen de un evento, que desde la
/// pantalla se parecen y nunca significan lo mismo: contestar que no está es una pérdida, y no
/// contestar es una avería. La regla es la de siempre; acá se comprueba que la portada y la
/// galería la heredan y no la reinventan.
/// </summary>
public sealed class EventImageDegradationTests : IClassFixture<FileStoreOutageApiDbFactory>
{
    private static readonly byte[] ImageBytes = SampleFiles.Jpeg();

    private readonly FileStoreOutageApiDbFactory _factory;
    private readonly HttpClient _client;

    public EventImageDegradationTests(FileStoreOutageApiDbFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Subir depende del almacenamiento. Falla con el código transitorio, y sobre todo no deja
    /// fila: lo que no se pudo guardar no existe, ni siquiera como archivo perdido.
    /// </summary>
    [Fact]
    public async Task UploadImage_Returns503AndCreatesNothing_WhenTheStoreIsUnreachable()
    {
        var eventId = await CreateEventAsync();

        using (_factory.Failing(FileStoreFault.Unavailable))
        {
            var response = await UploadAsync(eventId, "portada.jpg", kind: "Cover");

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadAsStringAsync()).ShouldContain("FILE_STORE_UNAVAILABLE");
        }

        (await ListAsync(eventId)).ShouldBeEmpty();
    }

    /// <summary>
    /// La avería no marca nada como perdido: al volver el almacenamiento, la imagen se baja igual
    /// que antes y la lista del operador sigue diciendo la verdad sobre qué falta de verdad.
    /// </summary>
    [Fact]
    public async Task GetFileContent_LeavesTheImageUnflaggedAndDownloadsAgainAfterwards_WhenTheStoreIsUnreachable()
    {
        var eventId = await CreateEventAsync();
        var uploaded = await UploadedAsync(eventId, "portada.jpg", kind: "Cover");

        using (_factory.Failing(FileStoreFault.Unavailable))
        {
            var response = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadAsStringAsync()).ShouldContain("FILE_STORE_UNAVAILABLE");
        }

        (await ListAsync(eventId)).ShouldHaveSingleItem().Missing.ShouldBeFalse();
        (await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content")).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Perdidos los bytes, la fila se sigue listando con su nombre y su fecha, marcada: una
    /// pantalla pinta un hueco donde había una foto, no una página rota.
    /// </summary>
    [Fact]
    public async Task ListImages_KeepsReturningTheImageFlaggedWithItsOriginalMetadata_WhenItIsMissing()
    {
        var eventId = await CreateEventAsync();
        var (uploaded, path) = await UploadedWithPathAsync(eventId, "portada.jpg", kind: "Cover");
        var before = (await ListAsync(eventId)).ShouldHaveSingleItem();

        File.Delete(path);
        var download = await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

        download.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await download.Content.ReadAsStringAsync()).ShouldContain("FILE_MISSING");

        var after = (await ListAsync(eventId)).ShouldHaveSingleItem();
        after.Missing.ShouldBeTrue();
        after.FileName.ShouldBe(before.FileName);
        after.CreatedAt.ShouldBe(before.CreatedAt);
        after.SizeBytes.ShouldBe(before.SizeBytes);
    }

    /// <summary>
    /// Y el listado de eventos tampoco esconde la portada perdida: la fila sigue trayéndola
    /// marcada, que es lo que deja pintar un marcador de posición en la grilla.
    /// </summary>
    [Fact]
    public async Task ListEvents_KeepsReturningTheCoverFlagged_WhenItIsMissing()
    {
        var eventId = await CreateEventAsync();
        var (uploaded, path) = await UploadedWithPathAsync(eventId, "portada.jpg", kind: "Cover");

        File.Delete(path);
        await _client.GetAsync($"v1/files/{uploaded.StoredFileId}/content");

        var row = await ListedEventAsync(eventId);

        row.Cover.ShouldNotBeNull();
        row.Cover!.Missing.ShouldBeTrue();
        row.Cover.FileName.ShouldBe("portada.jpg");
    }

    /// <summary>
    /// La ficha del archivo distingue las dos cosas por su cuenta: la avería no la tocó y la
    /// pérdida sí, y es lo que el operador lee para saber qué restaurar.
    /// </summary>
    [Fact]
    public async Task ListMissingFiles_ReturnsOnlyTheMissingImage_WhenAnotherOnlyTimedOut()
    {
        var eventId = await CreateEventAsync();
        var (lost, path) = await UploadedWithPathAsync(eventId, "perdida.jpg");
        var intact = await UploadedAsync(eventId, "intacta.jpg");

        using (_factory.Failing(FileStoreFault.Timeout))
            await _client.GetAsync($"v1/files/{intact.StoredFileId}/content");

        File.Delete(path);
        await _client.GetAsync($"v1/files/{lost.StoredFileId}/content");

        var page = await _client.GetFromJsonAsync<PagedResponse<MissingFileResponse>>(
            "v1/files/missing?pageSize=100");

        page!.Items.ShouldContain(f => f.Id == lost.StoredFileId);
        page.Items.ShouldNotContain(f => f.Id == intact.StoredFileId);
    }

    #region Helpers

    private async Task<Guid> CreateEventAsync()
    {
        var response = await _client.PostAsJsonAsync("v1/events", new CreateEventRequest
        {
            Type        = "Corporate",
            Name        = $"Roadshow Degradacion {Guid.NewGuid():N}",
            StartDate   = new DateOnly(2026, 9, 1),
            EndDate     = new DateOnly(2026, 9, 5),
            City        = "Santo Domingo",
            Country     = "Dominican Republic"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<EventListItemResponse> ListedEventAsync(Guid eventId)
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<EventListItemResponse>>(
            "v1/events?pageSize=100");
        return page!.Items.Single(e => e.Id == eventId);
    }

    private async Task<List<EventImageResponse>> ListAsync(Guid eventId)
    {
        var response = await _client.GetAsync($"v1/events/{eventId}/images");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<EventImageResponse>>())!;
    }

    /// <summary>
    /// Sube una imagen y devuelve, además de la respuesta, la ruta donde el proveedor dejó los
    /// bytes: es la que un test borra, y así nunca toca los archivos de otro.
    /// </summary>
    private async Task<(EventImageResponse Image, string Path)> UploadedWithPathAsync(
        Guid eventId,
        string fileName,
        string? kind = null)
    {
        var before = StoredFilePaths();
        var image = await UploadedAsync(eventId, fileName, kind);
        var path = StoredFilePaths().Except(before).ShouldHaveSingleItem();

        return (image, path);
    }

    private async Task<EventImageResponse> UploadedAsync(Guid eventId, string fileName, string? kind = null)
    {
        var response = await UploadAsync(eventId, fileName, kind);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EventImageResponse>())!;
    }

    private Task<HttpResponseMessage> UploadAsync(Guid eventId, string fileName, string? kind = null)
    {
        var file = new ByteArrayContent(ImageBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        var content = new MultipartFormDataContent { { file, "file", fileName } };

        if (kind is not null)
            content.Add(new StringContent(kind), "kind");

        return _client.PostAsync($"v1/events/{eventId}/images", content);
    }

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
