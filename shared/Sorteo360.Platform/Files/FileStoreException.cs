namespace Sorteo360.Platform.Files;

/// <summary>
/// Avería del almacenamiento de archivos. Los dos casos que hay que distinguir tienen su
/// propio tipo derivado; esta base cubre lo que no es ninguno de los dos, como una clave
/// corrupta, y por eso quien llama no la captura: sería un fallo de este servicio.
/// </summary>
public class FileStoreException : Exception
{
    public FileStoreException(string message)
        : base(message)
    {
    }

    public FileStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// El almacenamiento contestó y el archivo no está. Es permanente: alguien lo borró o el
/// antivirus lo puso en cuarentena.
/// </summary>
public sealed class FileNotFoundInStoreException : FileStoreException
{
    public FileNotFoundInStoreException(string storageKey)
        : base($"El archivo '{storageKey}' no está en el almacenamiento.")
    {
        StorageKey = storageKey;
    }

    public FileNotFoundInStoreException(string storageKey, Exception innerException)
        : base($"El archivo '{storageKey}' no está en el almacenamiento.", innerException)
    {
        StorageKey = storageKey;
    }

    /// <summary>Clave que se pidió y no está.</summary>
    public string StorageKey { get; }
}

/// <summary>
/// El almacenamiento no contestó o falló al contestar. Es transitorio, y no dice nada sobre
/// si el archivo existe: confundirlo con la ausencia daría por perdida una biblioteca entera
/// cada vez que se reinicia un servidor.
/// </summary>
public sealed class FileStoreUnavailableException : FileStoreException
{
    public FileStoreUnavailableException(string message)
        : base(message)
    {
    }

    public FileStoreUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
