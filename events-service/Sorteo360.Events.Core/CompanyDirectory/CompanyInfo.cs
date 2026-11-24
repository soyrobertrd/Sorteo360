namespace Sorteo360.Events.Core.CompanyDirectory;

/// <summary>
/// Una empresa del CRM tal como la ve este servicio al validar una escritura.
/// <para>
/// <c>IsActive</c> en <c>false</c> —la relación comercial está dormida— y <c>IsDeleted</c> en
/// <c>true</c> —la ficha se dio de baja— son los dos ejes que la lectura por id del CRM separa a
/// propósito, y son el sujeto de la regla que esta validación aplica: <b>las dos pasan</b>. La misma
/// comprobación protege la edición, y rechazar una empresa cerrada dejaría congelada para siempre la
/// ficha de un proveedor que todavía se contrata (ADR-0015). Viajan para que esa regla se lea en el
/// seam y cambiarla sea una línea, no un contrato nuevo.
/// </para>
/// <para>
/// El nombre no está: es para mostrar, y mostrar se resuelve contra la copia en caché del
/// directorio, no contra esta consulta.
/// </para>
/// </summary>
public sealed record CompanyInfo(Guid Id, bool IsActive, bool IsDeleted);
