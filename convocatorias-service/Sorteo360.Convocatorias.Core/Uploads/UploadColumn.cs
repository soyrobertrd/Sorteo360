namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Los cinco campos que este servicio sabe leer de una fila de la hoja. Es el vocabulario del
/// que habla <see cref="UploadColumnMap"/>, y no hay ningún otro: una columna que la hoja traiga
/// y no esté acá se ignora entera.
/// </summary>
public enum UploadColumn
{
    FirstName,
    LastName,
    Email,
    Phone,

    /// <summary>
    /// La empresa, que se guarda como <b>texto crudo</b> y no se resuelve contra nada. Crear
    /// empresas desde una columna con erratas es justo lo que el paso de revisión existe para
    /// evitar, y deduplicar por dominio está sin construir y tiene reglas propias (spec
    /// <c>05</c> §6).
    /// </summary>
    Company,
}
