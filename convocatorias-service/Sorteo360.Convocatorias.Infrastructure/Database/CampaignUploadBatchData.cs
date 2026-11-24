namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Fila de <c>campaign_upload_batch</c>: una subida de lista sobre una campaña.
/// <para>
/// <b>Es permanente.</b> Sus filas se borran al confirmar y estos siete enteros se quedan, porque
/// contestan lo que alguien pregunta después sin guardar el bulto, y porque son el registro de
/// quién confirmó, que es lo que hace aceptable el aplazamiento de <c>onBehalfOfUserId</c> en el
/// alta de contactos del CRM (spec <c>05</c> §2, spec <c>08</c> §4).
/// </para>
/// <para>
/// Única por campaña <b>entre las no confirmadas</b>, vía índice parcial
/// <c>WHERE confirmed_at IS NULL</c>: muchas confirmadas, una pendiente. Es el mismo modismo con
/// el que el CRM deja libre el correo de un contacto borrado.
/// </para>
/// </summary>
internal sealed class CampaignUploadBatchData
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public required string OriginalFileName { get; set; }

    /// <summary>Con qué codificación se leyó. Va a la vista, nunca en silencio.</summary>
    public required string DetectedEncoding { get; set; }

    public int RowCount { get; set; }

    public int MatchedCount { get; set; }

    public int UnmatchedCount { get; set; }

    public int SuppressedCount { get; set; }

    public int AlreadyRecipientCount { get; set; }

    public int DuplicateInFileCount { get; set; }

    public int MalformedCount { get; set; }

    public int CollapsedDuplicateCount { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    public Guid UploadedById { get; set; }

    /// <summary>Nulo mientras esté pendiente. Es la columna que mira el índice parcial.</summary>
    public DateTimeOffset? ConfirmedAt { get; set; }

    public Guid? ConfirmedById { get; set; }

    public CampaignData? Campaign { get; set; }

    public ICollection<CampaignUploadRowData> Rows { get; set; } = [];
}
