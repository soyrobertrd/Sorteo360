namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Una fila de datos de la hoja, ya clasificada, esperando a que alguien confirme.
/// <para>
/// <b>Estas filas no sobreviven a la confirmación</b>, que es donde está el bulto: se borran y
/// quedan las siete cuentas del lote. Mientras existen son el <b>cursor</b> de la confirmación:
/// cada una se marca aplicada en cuanto su efecto queda escrito, así que un reintento después de
/// un corte salta todo lo hecho y nadie se crea dos veces (spec <c>05</c> §6).
/// </para>
/// </summary>
public sealed record CampaignUploadRow
{
    public Guid Id { get; init; }

    public Guid BatchId { get; init; }

    /// <summary>La fila del archivo original, para que quien subió pueda ir a mirarla.</summary>
    public int SourceRowNumber { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    /// <summary>La dirección como vino, recortada y nada más: normalizar es del CRM (§5).</summary>
    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>
    /// La columna de empresa <b>como texto crudo</b>, para que una persona la reconcilie a mano
    /// en el CRM después. Crear empresas desde una columna con erratas es exactamente lo que el
    /// paso de revisión existe para evitar, y deduplicar empresas por dominio está sin construir.
    /// </summary>
    public string? CompanyRaw { get; init; }

    /// <summary>Ver <see cref="UploadRowBucket"/>.</summary>
    public string Bucket { get; init; }

    /// <summary>
    /// El código de motivo: uno de <see cref="MalformedRowReason"/> cuando la fila está mal
    /// formada, y el de supresión que calculó el CRM cuando está suprimida. Nulo en el resto.
    /// </summary>
    public string? ReasonCode { get; init; }

    /// <summary>
    /// El contacto del CRM: el que se encontró al revisar, o el que se creó al confirmar. Nulo
    /// mientras no haya ninguno.
    /// </summary>
    public Guid? ContactId { get; init; }

    /// <summary>
    /// Los números de fila del grupo que comparte correo, cuando el cubo es
    /// <c>duplicate_in_file</c>. Vacío en cualquier otro caso.
    /// </summary>
    public IReadOnlyList<int> DuplicateRowNumbers { get; init; }

    /// <summary>Cuántas filas idénticas se plegaron en esta.</summary>
    public int CollapsedDuplicates { get; init; }

    /// <summary>
    /// Cuándo quedó escrito el efecto de esta fila. Puesto significa hecho, y un reintento la
    /// salta. Es lo que hace que la confirmación sea idempotente <b>por fila</b> y no solo en
    /// conjunto.
    /// </summary>
    public DateTimeOffset? AppliedAt { get; init; }

    public CampaignUploadRow(
        Guid id,
        Guid batchId,
        int sourceRowNumber,
        string? firstName,
        string? lastName,
        string? email,
        string? phone,
        string? companyRaw,
        string bucket,
        string? reasonCode,
        Guid? contactId,
        IReadOnlyList<int> duplicateRowNumbers,
        int collapsedDuplicates,
        DateTimeOffset? appliedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentNullException.ThrowIfNull(duplicateRowNumbers);
        ArgumentOutOfRangeException.ThrowIfNegative(collapsedDuplicates);

        if (!UploadRowBucket.All.Contains(bucket))
            throw new ArgumentException($"Cubo no admitido: '{bucket}'.", nameof(bucket));

        Id = id;
        BatchId = batchId;
        SourceRowNumber = sourceRowNumber;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        CompanyRaw = companyRaw;
        Bucket = bucket;
        ReasonCode = reasonCode;
        ContactId = contactId;
        DuplicateRowNumbers = duplicateRowNumbers;
        CollapsedDuplicates = collapsedDuplicates;
        AppliedAt = appliedAt;
    }

    /// <summary>Nombre completo tal como lo trae la hoja, para la fila de destinatario que salga de acá.</summary>
    public string FullName => string.Join(' ', new[] { FirstName, LastName }
        .Where(part => !string.IsNullOrWhiteSpace(part)));
}
