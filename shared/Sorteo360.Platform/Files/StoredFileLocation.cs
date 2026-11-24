namespace Sorteo360.Platform.Files;

/// <summary>
/// Lo que el almacenamiento sabe de un archivo recién guardado. Todo lo demás —para qué es,
/// quién lo subió— es del dominio y vive en su propia fila.
/// </summary>
/// <param name="Provider">Proveedor que guardó los bytes, tal como se registra en la fila.</param>
/// <param name="StorageKey">Clave opaca con la que se vuelve a pedir el contenido.</param>
/// <param name="SizeBytes">Tamaño real de lo guardado, contado al escribir y no declarado por nadie.</param>
/// <param name="Checksum">SHA-256 del contenido en hexadecimal minúsculo, calculado al vuelo.</param>
public sealed record StoredFileLocation(
    string Provider,
    string StorageKey,
    long SizeBytes,
    string Checksum);
