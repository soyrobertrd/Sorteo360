namespace Sorteo360.Convocatorias.Core.Shared;

/// <summary>Las categorías de ServiceSupplier que events-service acepta.</summary>
/// <remarks>
/// Es copia de <c>ServiceSupplierCategory</c> de events-service, que es el dueño del catálogo; si allá
/// cambia, cambia aquí. La carga de proveedores la usa para contestar un 400 de campo, porque un 400
/// de events-service se leería como "events-service no está" y el envío fallaría en cada intento.
/// </remarks>
public static class ServiceSupplierCategories
{
    /// <summary>La única categoría que admite aforos, como principal o como secundaria.</summary>
    public const string Venue = "Venue";

    /// <summary>Las quince categorías, comparadas sin distinguir mayúsculas como en events-service.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Venue, "Ambientacion", "Audiovisuales", "Seguridad", "Traductores", "Edecanes", "Alimentacion",
        "Transporte", "Fotografia", "Produccion", "Tecnologia", "Mobiliario", "Entretenimiento", "Limpieza",
        "Logistica",
    };
}
