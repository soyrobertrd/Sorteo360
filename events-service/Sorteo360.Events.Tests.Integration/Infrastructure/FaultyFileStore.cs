using Sorteo360.Platform.Files;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>Avería que <see cref="FaultyFileStore"/> tiene armada en cada momento.</summary>
public enum FileStoreFault
{
    /// <summary>Sin avería: el almacenamiento de verdad contesta.</summary>
    None,

    /// <summary>El almacenamiento no contesta o contesta con un error.</summary>
    Unavailable,

    /// <summary>El almacenamiento tarda más de lo que se le espera y la llamada se corta.</summary>
    Timeout,

    /// <summary>
    /// El almacenamiento acepta la primera escritura y no contesta a las siguientes; leer y borrar
    /// siguen andando. Es la caída a mitad de un guardado de varios archivos, cuando ya hay bytes
    /// que descartar.
    /// </summary>
    UnavailableAfterFirstSave,
}

/// <summary>
/// Envoltorio del almacenamiento real que permite tirarlo abajo desde un test.
/// <para>
/// No reemplaza al proveedor: lo envuelve. Cuando no hay avería armada, escribe, lee y borra el
/// proveedor de verdad, que es lo que el resto de los tests ejercita. La avería es lo único que
/// no se puede provocar de otra forma — un directorio temporal no se cae ni tarda —, y sin
/// poder provocarla no habría manera de comprobar lo que este servicio promete: que un
/// almacenamiento caído no da por perdido ningún archivo.
/// </para>
/// </summary>
public sealed class FaultyFileStore : IFileStore
{
    /// <summary>
    /// Lo que espera un proveedor antes de darse por vencido. Corto porque acá nada tiene que
    /// contestar: lo que se ejercita es que el plazo se venza de verdad.
    /// </summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromMilliseconds(50);

    private readonly IFileStore _inner;

    private FileStoreFault _fault = FileStoreFault.None;

    /// <summary>Escrituras desde que se armó la avería, para la que deja pasar solo la primera.</summary>
    private int _savesSinceArmed;

    /// <summary>El nombre cuya próxima escritura falla una sola vez, o nulo si no hay ninguno.</summary>
    private string? _failNextSaveOf;

    public FaultyFileStore(IFileStore inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Avería armada. Un test la enciende, hace su llamada y la vuelve a apagar.</summary>
    public FileStoreFault Fault
    {
        get => _fault;
        set
        {
            _fault = value;
            Interlocked.Exchange(ref _savesSinceArmed, 0);
        }
    }


    /// <inheritdoc />
    public async Task<StoredFileLocation> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)
    {
        if (_failNextSaveOf is not null && _failNextSaveOf == fileName)
        {
            _failNextSaveOf = null;
            throw Unavailable();
        }

        if (Fault is FileStoreFault.UnavailableAfterFirstSave && Interlocked.Increment(ref _savesSinceArmed) > 1)
            throw Unavailable();

        await FailIfFaultedAsync(cancellationToken);
        return await _inner.SaveAsync(content, fileName, contentType, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        await FailIfFaultedAsync(cancellationToken);
        return await _inner.OpenReadAsync(storageKey, cancellationToken);
    }

    /// <summary>
    /// Hace fallar una sola vez la próxima escritura de <paramref name="fileName"/>; las demás
    /// escrituras, y las siguientes de ese nombre, pasan. Es el almacenamiento que tropieza una vez y
    /// se recupera enseguida.
    /// </summary>
    public void FailNextSaveOf(string fileName) => _failNextSaveOf = fileName;

    /// <summary>Arma una avería y la desarma al salir del bloque <c>using</c>.</summary>
    public IDisposable Arm(FileStoreFault fault) => new FaultScope(this, fault);

    /// <inheritdoc />
    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        await FailIfFaultedAsync(cancellationToken);
        await _inner.DeleteAsync(storageKey, cancellationToken);
    }

    /// <summary>
    /// Falla como fallaría un proveedor real ante cada avería.
    /// <para>
    /// El timeout no se finge: se espera de verdad hasta que el plazo se vence y lo que sale de
    /// ahí es la cancelación real, traducida como la traduce un proveedor sobre HTTP. Es la
    /// única forma de que las dos averías sean dos caminos y no el mismo <c>throw</c> con dos
    /// mensajes. Que las dos terminen en la misma excepción del seam es el punto: quien llama no
    /// tiene por qué distinguir "no contestó" de "tardó demasiado", y sí tiene que distinguir
    /// las dos de "el archivo no está".
    /// </para>
    /// </summary>
    private async Task FailIfFaultedAsync(CancellationToken cancellationToken)
    {
        switch (Fault)
        {
            case FileStoreFault.Unavailable:
                throw Unavailable();

            case FileStoreFault.Timeout:
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    deadline.CancelAfter(Deadline);

                    try
                    {
                        // Nada va a completar esta espera: es un almacenamiento que se quedó callado.
                        await Task.Delay(Timeout.Infinite, deadline.Token);
                    }
                    catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new FileStoreUnavailableException(
                            "El almacenamiento de archivos no contestó a tiempo.", exception);
                    }
                }

                return;

            case FileStoreFault.UnavailableAfterFirstSave:
            case FileStoreFault.None:
            default:
                return;
        }
    }

    /// <summary>La excepción con que un proveedor real dice que no contestó.</summary>
    private static FileStoreUnavailableException Unavailable() => new(
        "El almacenamiento de archivos no contestó.",
        new IOException("connection refused"));

    private sealed class FaultScope : IDisposable
    {
        private readonly FaultyFileStore _store;

        public FaultScope(FaultyFileStore store, FileStoreFault fault)
        {
            _store = store;
            _store.Fault = fault;
        }

        public void Dispose() => _store.Fault = FileStoreFault.None;
    }
}
