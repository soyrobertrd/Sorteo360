namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// En qué acaba cada fila de datos de una hoja. Los seis cubos de la spec <c>05</c> §4 son
/// <b>disjuntos</b> y se evalúan en el orden en que están declarados, gana el primero, así que
/// las cuentas cuadran contra el número de filas sin que nadie tenga que restar.
/// <para>
/// <c>data rows = matched + unmatched + suppressed + already_a_recipient + duplicate_in_file
/// + malformed + collapsed_exact_duplicates</c>, y ese último sumando no es un cubo sino las
/// filas que se plegaron en otra (§5).
/// </para>
/// </summary>
public static class UploadRowBucket
{
    /// <summary>Le falta el correo, o el correo no es una dirección, o le falta el nombre.</summary>
    public const string Malformed = "malformed";

    /// <summary>
    /// Comparte correo con otra fila del mismo archivo y las dos no dicen lo mismo. Ninguna se
    /// crea: el archivo se contradice a sí mismo y no tenemos con qué desempatar (§5).
    /// </summary>
    public const string DuplicateInFile = "duplicate_in_file";

    /// <summary>
    /// Está en el CRM y ya es destinatario de esta campaña. <b>Gana a
    /// <see cref="Suppressed"/> a propósito</b>: alguien que se dio de baja después de la
    /// selección ya está en la campaña, y quitarlo acá duplicaría el trabajo que la
    /// recomprobación del lanzamiento ya hace en el único momento en que es autoritativa.
    /// </summary>
    public const string AlreadyARecipient = "already_a_recipient";

    /// <summary>
    /// Está en el CRM y el CRM dice que no se le puede escribir. Lleva el código de motivo que
    /// calculó el CRM. Solo filas <b>encontradas</b>: una fila que no está en el CRM no puede
    /// estar suprimida.
    /// </summary>
    public const string Suppressed = "suppressed";

    /// <summary>Está en el CRM y se le puede escribir. Se enlaza, no se crea.</summary>
    public const string Matched = "matched";

    /// <summary>No está en el CRM. Se crea al confirmar, y después se enlaza.</summary>
    public const string Unmatched = "unmatched";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Malformed, DuplicateInFile, AlreadyARecipient, Suppressed, Matched, Unmatched
    };

    /// <summary>
    /// Si confirmar hace algo con esta fila. Las dos primeras no se confirman nunca, y
    /// <see cref="AlreadyARecipient"/> es un no-op declarado: se cuenta, y no se toca nada.
    /// </summary>
    public static bool IsConfirmable(string bucket) =>
        bucket is Matched or Unmatched or Suppressed;

    /// <summary>
    /// Si confirmar esta fila materializa un destinatario. <see cref="Unmatched"/> cuenta porque
    /// el cubo dice de dónde salió el contacto —lo creamos nosotros— y no qué se escribe con él:
    /// una vez creado, se enlaza igual que uno encontrado.
    /// </summary>
    public static bool CreatesRecipient(string bucket) => bucket is Matched or Unmatched;
}
