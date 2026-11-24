namespace Sorteo360.Events.Core.CompanyDirectory;

/// <summary>
/// Una persona de contacto de la empresa que se contrata, tal como la ve este servicio: a quién
/// llamar por una cotización, sin salir de la ficha del proveedor.
/// <para>
/// No está en <see cref="CompanySnapshot"/> a propósito. La copia del directorio es la identidad
/// mínima de todas las empresas y se sirve entera en cada listado; los contactos se piden por id en
/// la única pantalla que los muestra (ADR-0015).
/// </para>
/// <para>
/// El consentimiento de mercadeo del CRM no viaja, y no es un olvido: acá no se decide a quién
/// escribirle una campaña, sino a quién llamar por un trabajo ya contratado. Quien lo retiró
/// aparece igual, así que un campo que nadie puede usar para filtrar solo invitaría a filtrar mal.
/// </para>
/// </summary>
/// <param name="Id">El id de la persona en el CRM, que es por donde se abre su ficha allá.</param>
public sealed record CompanyContact(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? JobTitle);
