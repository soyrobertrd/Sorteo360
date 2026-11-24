namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Fila de <c>campaign_upload_row</c>: una fila de datos de la hoja, clasificada y esperando a
/// que alguien confirme.
/// <para>
/// <b>No sobrevive a la confirmación.</b> Acá está el bulto —cuatrocientas filas para decir que
/// doce estaban mal— y las siete cuentas del lote contestan lo mismo sin guardarlas. Mientras
/// existe es el <b>cursor</b>: <see cref="AppliedAt"/> puesto significa hecho, y un reintento la
/// salta (spec <c>05</c> §2 y §6).
/// </para>
/// </summary>
internal sealed class CampaignUploadRowData
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    /// <summary>La fila del archivo original, contando el encabezado.</summary>
    public int SourceRowNumber { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    /// <summary>La dirección recortada y por lo demás cruda: normalizar es del CRM.</summary>
    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>La empresa como texto, para que una persona la reconcilie en el CRM después.</summary>
    public string? CompanyRaw { get; set; }

    public required string Bucket { get; set; }

    public string? ReasonCode { get; set; }

    public Guid? ContactId { get; set; }

    /// <summary>
    /// Los números de fila del grupo que comparte correo. Arreglo y no texto: es una lista de
    /// enteros y Postgres sabe guardarla como tal.
    /// </summary>
    public int[] DuplicateRowNumbers { get; set; } = [];

    public int CollapsedDuplicates { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    public CampaignUploadBatchData? Batch { get; set; }
}
