namespace Sorteo360.Platform.Files;

/// <summary>
/// Configuración del almacenamiento de archivos (sección <c>FileStore</c>). El proveedor se
/// elige acá y no por el entorno de ejecución: cualquier entorno puede apuntar a cualquiera de
/// los dos, que es lo que permite probar el de producción en local sin disfrazar el entorno.
/// </summary>
public sealed record FileStoreSettings
{
    public static string SectionName => "FileStore";

    /// <summary>Proveedor a registrar. Hoy solo <c>filesystem</c>; sin valor por omisión a propósito.</summary>
    public string? Provider { get; init; }

    /// <summary>Ajustes del proveedor de sistema de archivos; se ignoran si no es el elegido.</summary>
    public FilesystemFileStoreSettings Filesystem { get; init; } = new();
}

/// <summary>
/// Ajustes del proveedor de sistema de archivos (<c>FileStore__Filesystem__*</c>).
/// </summary>
public sealed record FilesystemFileStoreSettings
{
    /// <summary>Carpeta raíz bajo la que se escriben todos los archivos.</summary>
    public string? RootPath { get; init; }
}
