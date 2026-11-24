using Sorteo360.Platform.Errors;

namespace Sorteo360.Events.Core.Errors;

/// <summary>
/// Fallos al validar contra el directorio de empresas del CRM, que es su única autoridad
/// (ADR-0015).
/// <para>
/// El rechazo de id es <b>conflicto</b> y no petición inválida: la empresa se elige de un
/// desplegable que se llena desde el CRM, así que un id malo de un cliente que se porta bien
/// significa que la empresa no existe o que alguien la borró entre que se cargó el formulario y se
/// envió. Decirle que mandó algo mal formado sería mentirle.
/// </para>
/// </summary>
public static class CompanyErrors
{
    /// <summary>
    /// El id de empresa no existe en el CRM. Un solo motivo: la lectura por id resuelve también las
    /// empresas dormidas y las dadas de baja, precisamente para que editar la ficha de un proveedor
    /// viejo siga siendo posible, así que lo único que se rechaza acá es un id que nunca existió.
    /// </summary>
    public static BaseError CompanyNotAvailable() => new(
        "La empresa indicada no existe en el CRM.",
        "COMPANY_NOT_AVAILABLE",
        ErrorKind.Conflict);

    /// <summary>
    /// El contacto no existe, o no tiene una vinculación vigente con esa empresa. Un solo código para
    /// los dos casos, como <see cref="OptErrors.ContactNotAvailable"/>: al cliente le sirve lo mismo
    /// —volver a cargar los contactos de la empresa y elegir otro—.
    /// </summary>
    public static BaseError ContactNotAvailable() => new(
        "El contacto indicado no existe o no tiene una vinculación vigente con esa empresa.",
        "COMPANY_CONTACT_NOT_AVAILABLE",
        ErrorKind.Conflict);

    /// <summary>
    /// El CRM no contestó, así que no hay con qué validar y la escritura se rechaza. Este servicio
    /// está sano —de ahí el 503 y no un 500—, y quien escribe no pudo haber elegido la empresa sin
    /// el CRM, así que fallar es coherente con lo que esa persona acaba de vivir.
    /// <para>
    /// Lleva el mismo código que <see cref="OptErrors.CrmUnavailable"/> a propósito: es el mismo CRM
    /// caído, y un cliente que quiera reintentar no necesita saber qué directorio se estaba
    /// consultando (ADR-0015). Solo cambia la prosa, que nunca es contrato.
    /// </para>
    /// </summary>
    public static BaseError CrmUnavailable() => new(
        "El directorio de empresas no está disponible en este momento; intente nuevamente.",
        "CRM_UNAVAILABLE",
        ErrorKind.Unavailable);
}
