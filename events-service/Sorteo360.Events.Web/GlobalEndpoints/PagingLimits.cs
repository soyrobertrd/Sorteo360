namespace Sorteo360.Events.Web.GlobalEndpoints;

/// <summary>
/// Los números de la paginación, en un solo lugar. Estaban repetidos en cada archivo de rutas, y
/// repetidos es como una página de eventos y una de archivos terminan trayendo cantidades
/// distintas sin que nadie lo haya decidido.
/// </summary>
public static class PagingLimits
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;

    /// <summary>Primera página. Coincide hoy con el default, pero no es lo mismo: uno es dónde
    /// empieza la numeración y el otro es a dónde ir cuando nadie la pidió.</summary>
    public const int MinPage = 1;

    /// <summary>Página más chica que tiene sentido pedir.</summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// Techo del tamaño de página. Es lo que impide que una sola petición se traiga la tabla
    /// entera y con ella toda la memoria del servicio.
    /// </summary>
    public const int MaxPageSize = 100;
}
