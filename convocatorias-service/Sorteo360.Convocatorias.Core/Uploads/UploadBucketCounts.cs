namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Las siete cuentas de un lote. <b>Son lo que sobrevive a la confirmación</b>: las filas se
/// borran y estos siete enteros se quedan, porque contestan lo que alguien pregunta después
/// —"subimos 340 filas y 12 estaban mal"— sin guardar 340 filas para decirlo (spec <c>05</c> §2).
/// <para>
/// Los seis cubos son disjuntos, así que sumados con las filas plegadas dan exactamente las
/// filas de datos del archivo. Que cuadren no es decoración: es lo que permite creerse el
/// resumen sin volver a abrir la hoja.
/// </para>
/// </summary>
public sealed record UploadBucketCounts
{
    public int Matched { get; init; }

    public int Unmatched { get; init; }

    public int Suppressed { get; init; }

    public int AlreadyARecipient { get; init; }

    public int DuplicateInFile { get; init; }

    public int Malformed { get; init; }

    /// <summary>Filas idénticas que se plegaron en otra. No es un cubo: es lo que ya no está.</summary>
    public int CollapsedExactDuplicates { get; init; }

    /// <summary>La suma de las siete, que tiene que ser el número de filas de datos del archivo.</summary>
    public int Total =>
        Matched + Unmatched + Suppressed + AlreadyARecipient
        + DuplicateInFile + Malformed + CollapsedExactDuplicates;

    /// <summary>
    /// Cuenta los cubos de un conjunto de filas. <b>Es el único sitio donde se tallan</b>, y lo
    /// usan tanto la revisión como el cierre de la carga: las cuentas se recalculan al confirmar
    /// porque un 409 puede haber movido alguna fila de cubo, y dos tallas separadas acabarían
    /// discrepando el día que aparezca un séptimo cubo.
    /// </summary>
    /// <param name="buckets">El cubo de cada fila que se guarda.</param>
    /// <param name="collapsedExactDuplicates">Las filas idénticas que se plegaron y ya no están.</param>
    public static UploadBucketCounts From(IEnumerable<string> buckets, int collapsedExactDuplicates)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        var tally = buckets
            .GroupBy(bucket => bucket, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new UploadBucketCounts
        {
            Matched = tally.GetValueOrDefault(UploadRowBucket.Matched),
            Unmatched = tally.GetValueOrDefault(UploadRowBucket.Unmatched),
            Suppressed = tally.GetValueOrDefault(UploadRowBucket.Suppressed),
            AlreadyARecipient = tally.GetValueOrDefault(UploadRowBucket.AlreadyARecipient),
            DuplicateInFile = tally.GetValueOrDefault(UploadRowBucket.DuplicateInFile),
            Malformed = tally.GetValueOrDefault(UploadRowBucket.Malformed),
            CollapsedExactDuplicates = collapsedExactDuplicates,
        };
    }
}
