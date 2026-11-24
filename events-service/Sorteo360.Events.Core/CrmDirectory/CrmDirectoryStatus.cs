namespace Sorteo360.Events.Core.CrmDirectory;

/// <summary>
/// Qué tan confiable es lo que el CRM acaba de devolver para mostrar. Viaja al cliente en la
/// cabecera <c>X-Crm-Directory-Status</c> para que un nombre ausente se distinga de uno vacío.
/// <para>
/// El estado es del CRM entero y no de un directorio en particular: todo lo que este servicio
/// resuelve para mostrar sale del mismo CRM, así que sus caídas son la misma caída. Un estado por
/// directorio daría a entender una independencia que no existe (ADR-0015).
/// </para>
/// <para>
/// El orden de los valores es el de gravedad creciente, y de eso depende
/// <see cref="CrmDirectoryStatusContext"/> para quedarse con el peor de la petición.
/// </para>
/// </summary>
public enum CrmDirectoryStatus
{
    /// <summary>La respuesta salió de una copia vigente.</summary>
    Ok,

    /// <summary>
    /// El CRM no respondió y se sirvió la última copia buena, sin importar su antigüedad. Los
    /// nombres son reales aunque puedan haber cambiado: nunca se falla una lectura por un nombre.
    /// </summary>
    Stale,

    /// <summary>
    /// Nunca hubo una copia buena que servir: este servicio arrancó con el CRM caído y la caché
    /// vive en memoria, así que cada reinicio puede volver a este estado. Los nombres van nulos y
    /// el cliente muestra un aviso, en lugar de una pantalla de celdas en blanco.
    /// </summary>
    Unavailable
}
