namespace Sorteo360.Convocatorias.Web.CampaignEndpoints;

/// <summary>
/// El resumen de una carga: qué archivo llegó, cómo se leyó, y en qué acaba cada fila.
/// <para>
/// <b>Las siete cuentas suman <c>rowCount</c></b>. Que cuadren es lo que permite creerse el
/// resumen sin volver a abrir la hoja.
/// </para>
/// </summary>
public sealed record RecipientUploadResponse
{
    public Guid Id { get; init; }

    public Guid CampaignId { get; init; }

    public required string OriginalFileName { get; init; }

    /// <summary>
    /// Con qué codificación se leyó el archivo. Sale a la vista a propósito: "windows-1252" al
    /// lado de un nombre con la tilde rota es una pista, y un respaldo silencioso no lo es.
    /// </summary>
    public required string DetectedEncoding { get; init; }

    /// <summary>Filas de datos del archivo, sin contar el encabezado.</summary>
    public int RowCount { get; init; }

    public required UploadCountsResponse Counts { get; init; }

    /// <summary>Cuántas filas hacen algo al confirmar. Cero es un desenlace correcto.</summary>
    public int ConfirmableCount { get; init; }

    public DateTimeOffset UploadedAt { get; init; }

    public Guid UploadedById { get; init; }

    public DateTimeOffset? ConfirmedAt { get; init; }

    public Guid? ConfirmedById { get; init; }

    /// <summary>
    /// Las filas revisadas, en el orden del archivo. Vienen al revisar y ya no al confirmar,
    /// porque confirmar las borra: lo que queda son las cuentas.
    /// </summary>
    public IReadOnlyList<RecipientUploadRowResponse> Rows { get; init; } = [];
}

/// <summary>Las siete cuentas de §4.</summary>
public sealed record UploadCountsResponse
{
    public int Matched { get; init; }

    public int Unmatched { get; init; }

    public int Suppressed { get; init; }

    public int AlreadyARecipient { get; init; }

    public int DuplicateInFile { get; init; }

    public int Malformed { get; init; }

    /// <summary>Filas idénticas plegadas en otra. No es un cubo: es lo que ya no está.</summary>
    public int CollapsedExactDuplicates { get; init; }
}

/// <summary>Una fila revisada, tal como la pinta la pantalla de revisión.</summary>
public sealed record RecipientUploadRowResponse
{
    /// <summary>La fila del archivo original, contando el encabezado.</summary>
    public int SourceRowNumber { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>La empresa como texto crudo. No se resuelve contra nada ni crea nada.</summary>
    public string? Company { get; init; }

    /// <summary>Uno de los seis cubos.</summary>
    public required string Bucket { get; init; }

    /// <summary>
    /// El código de motivo: el de la fila mal formada, o el de supresión que calculó el CRM. El
    /// texto en español lo pinta quien consume, a partir de este código.
    /// </summary>
    public string? ReasonCode { get; init; }

    public Guid? ContactId { get; init; }

    /// <summary>
    /// Las filas del grupo que comparte correo, cuando el cubo es <c>duplicate_in_file</c>. El
    /// cubo las nombra todas para que quien subió pueda ir a mirarlas.
    /// </summary>
    public IReadOnlyList<int> DuplicateRowNumbers { get; init; } = [];

    /// <summary>Cuántas filas idénticas se plegaron en esta.</summary>
    public int CollapsedDuplicates { get; init; }
}
