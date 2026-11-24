namespace Sorteo360.Events.Core.CompanyDirectory;

/// <summary>
/// La identidad mínima de una empresa del CRM, tal como la sirve la copia del directorio para
/// mostrar.
/// <para>
/// Son cinco campos y ningún otro a propósito: esto es lo que se cachea entero, no la ficha de la
/// empresa. El domicilio, el teléfono y los contactos se piden por id en la única pantalla que los
/// necesita, y a la escala de MITUR esta copia son cientos de kilobytes (ADR-0015).
/// </para>
/// <para>
/// <c>IsActive</c> acá es <b>un solo bit</b> —si la empresa todavía se puede elegir— y vale
/// <c>false</c> tanto para una dormida como para una dada de baja. No es el <c>IsHireable</c> del
/// proveedor: el del CRM describe la relación comercial con la empresa y el otro dice "no volverla
/// a contratar", las dos cosas son ciertas a la vez, y por eso ninguna se hereda de la otra. Los
/// dos ejes separados viven en <see cref="CompanyInfo"/>, que es la consulta con la que se valida
/// una escritura.
/// </para>
/// </summary>
public sealed record CompanySnapshot(
    Guid Id,
    string Name,
    string Country,
    string? City,
    bool IsActive);
