namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Por qué una fila quedó en <see cref="UploadRowBucket.Malformed"/>. Un código por fila, para
/// que la pantalla de revisión pueda decir qué arreglar en cuál.
/// <para>
/// <b>Un fallo de fila nunca tumba el archivo.</b> Rechazar cuatrocientas filas por una errata
/// es como se acaba editando hojas de cálculo a mano de madrugada; y un archivo donde
/// <i>todas</i> las filas están mal sigue siendo una subida correcta con cero filas
/// confirmables, porque es así como alguien descubre que se equivocó de columnas (spec
/// <c>05</c> §4).
/// </para>
/// </summary>
public static class MalformedRowReason
{
    /// <summary>
    /// La fila no trae dirección. No se crea un contacto sin correo desde una carga de campaña:
    /// sería meter en el sistema de registro a alguien que no puede hacer aquello para lo que se
    /// le subió.
    /// <para>
    /// <b>No es el <c>no_email</c> de las supresiones del CRM</b>, aunque se escriba igual: aquel
    /// habla de un contacto conocido y este de una fila que todavía no es contacto (spec
    /// <c>05</c> §5). <b>Provisional</b>: si las listas reales del cliente traen gente sin
    /// dirección, esta es la primera regla que hay que volver a mirar.
    /// </para>
    /// </summary>
    public const string MissingEmail = "missing_email";

    /// <summary>Trae algo en la columna del correo, y ese algo no es una dirección.</summary>
    public const string InvalidEmail = "invalid_email";

    /// <summary>
    /// Le falta el nombre, o le falta el apellido. <b>Hacen falta los dos</b>, y no porque un
    /// destinatario sin apellido no se pueda saludar: el CRM exige ambos al crear un contacto, así
    /// que aceptar la fila obligaría a rellenar el hueco con un texto inventado, y meter un
    /// "(sin apellido)" en el sistema de registro es exactamente la basura que el paso de revisión
    /// existe para evitar.
    /// <para>
    /// <b>Provisional, como el mapeo de columnas.</b> Si la hoja real del cliente trae una sola
    /// columna de nombre completo, todas sus filas salen mal formadas —que es una subida correcta
    /// con cero filas confirmables, y así es como se ve que el mapeo no encaja (§4)— y lo que hay
    /// que cambiar es <see cref="UploadColumnMap"/>.
    /// </para>
    /// </summary>
    public const string MissingName = "missing_name";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MissingEmail, InvalidEmail, MissingName
    };
}
