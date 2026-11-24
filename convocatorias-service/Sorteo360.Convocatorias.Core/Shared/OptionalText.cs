namespace Sorteo360.Convocatorias.Core.Shared;

/// <summary>
/// Cómo se guarda un campo de texto que la persona podía dejar en blanco.
/// <para>
/// Vive acá por el mismo motivo que <see cref="EmailAddressShape"/> y
/// <see cref="PersonNameShape"/>: el teléfono de una inscripción viaja a <b>dos sitios</b> —la
/// fila del registro y el alta del contacto en el CRM— y esos dos tienen que decir lo mismo.
/// Dos copias de este recorte que se separaran dejarían una ficha del CRM con un espacio que la
/// fila no tiene, y nadie lo vería hasta que alguien comparara las dos a mano.
/// </para>
/// </summary>
public static class OptionalText
{
    /// <summary>
    /// Recortado, y <b>nulo si lo que queda no dice nada</b>. Un campo con solo espacios es un
    /// campo que la persona no llenó, y guardarlo como cadena vacía sería un segundo "vacío" que
    /// toda lectura posterior tendría que conocer.
    /// </summary>
    /// <param name="value">Lo que escribió, tal cual.</param>
    public static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
