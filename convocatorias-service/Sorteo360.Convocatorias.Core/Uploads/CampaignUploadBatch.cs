namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Una subida de lista sobre una campaña en borrador: qué archivo llegó, cómo se leyó y en qué
/// acabó cada fila, en cuentas.
/// <para>
/// <b>El archivo no se guarda.</b> Sus bytes se parsean y se tiran. Guardarlos haría de
/// convocatorias un consumidor de ADR-0014 a cambio de una auditoría que nadie pidió, y
/// <b>las filas revisadas son mejor registro</b> que los bytes: son lo que alguien confirmó,
/// mientras que los bytes son solo lo que mandó.
/// </para>
/// <para>
/// <b>Esta fila es permanente y las suyas no.</b> Sobrevive a la confirmación porque otra
/// decisión se apoya en ella: la spec <c>08</c> §4 aplaza <c>onBehalfOfUserId</c> en el alta de
/// contactos, así que todo contacto subido queda atribuido a
/// <c>service-account-convocatorias-service</c>, y ese aplazamiento solo es aceptable porque
/// este lote registra <b>quién confirmó</b>. Borrarlo retiraría en silencio lo que hizo
/// aceptable el aplazamiento, y el cambio posterior se rellena justo desde acá. Seis enteros y
/// dos identidades es todo lo que cuesta.
/// </para>
/// <para>
/// <b>Una segunda subida reemplaza a la primera</b>, y la restricción lo dice con precisión:
/// única por campaña <i>entre las no confirmadas</i>. Muchas confirmadas, una sin confirmar.
/// Añadir en vez de reemplazar obligaría a reconciliar dos lotes sin confirmar entre sí, que es
/// el problema de §5 a otro grano, y el motivo real por el que alguien sube dos veces es que el
/// primer archivo estaba mal. Dos listas de verdad se componen confirmando la primera y subiendo
/// la segunda, porque confirmar libera el sitio.
/// </para>
/// </summary>
public sealed record CampaignUploadBatch
{
    public Guid Id { get; init; }

    public Guid CampaignId { get; init; }

    /// <summary>El nombre que traía el archivo, lo único que le sirve a una persona para reconocerlo.</summary>
    public string OriginalFileName { get; init; }

    /// <summary>
    /// Con qué codificación se leyó. Va a la vista a propósito: "leído como Windows-1252" al lado
    /// de un nombre destrozado es una pista, y un respaldo silencioso es mojibake en el CRM.
    /// </summary>
    public string DetectedEncoding { get; init; }

    /// <summary>Filas de datos del archivo, sin contar el encabezado.</summary>
    public int RowCount { get; init; }

    public UploadBucketCounts Counts { get; init; }

    public DateTimeOffset UploadedAt { get; init; }

    public Guid UploadedById { get; init; }

    /// <summary>Nulo mientras el lote esté pendiente. Es lo que la restricción parcial mira.</summary>
    public DateTimeOffset? ConfirmedAt { get; init; }

    /// <summary>Quién confirmó. Es el dato que sostiene el aplazamiento de <c>onBehalfOfUserId</c>.</summary>
    public Guid? ConfirmedById { get; init; }

    public CampaignUploadBatch(
        Guid id,
        Guid campaignId,
        string originalFileName,
        string detectedEncoding,
        int rowCount,
        UploadBucketCounts counts,
        DateTimeOffset uploadedAt,
        Guid uploadedById,
        DateTimeOffset? confirmedAt,
        Guid? confirmedById)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(detectedEncoding);
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);

        if (counts.Total != rowCount)
        {
            throw new ArgumentException(
                $"Las cuentas del lote suman {counts.Total} y el archivo trajo {rowCount} filas de datos.",
                nameof(counts));
        }

        Id = id;
        CampaignId = campaignId;
        OriginalFileName = originalFileName;
        DetectedEncoding = detectedEncoding;
        RowCount = rowCount;
        Counts = counts;
        UploadedAt = uploadedAt;
        UploadedById = uploadedById;
        ConfirmedAt = confirmedAt;
        ConfirmedById = confirmedById;
    }

    /// <summary>Todavía ocupa el único sitio de lote sin confirmar de su campaña.</summary>
    public bool IsPending => ConfirmedAt is null;

    /// <summary>
    /// Cuántas filas se vuelven destinatarias al confirmar. Un archivo entero mal formado da cero,
    /// y sigue siendo una subida correcta: es así como alguien descubre que se equivocó de
    /// columnas.
    /// <para>
    /// <b>Las suprimidas no cuentan</b>, aunque confirmar sí escriba algo por ellas: lo que
    /// escribe es una exclusión, y la tabla de §4 las marca como no confirmables por eso mismo.
    /// Sumarlas diría "tres filas van a entrar" cuando entran dos.
    /// </para>
    /// </summary>
    public int ConfirmableCount => Counts.Matched + Counts.Unmatched;
}
