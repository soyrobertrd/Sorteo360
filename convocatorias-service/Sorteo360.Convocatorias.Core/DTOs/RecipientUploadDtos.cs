using Sorteo360.Convocatorias.Core.Uploads;

namespace Sorteo360.Convocatorias.Core.DTOs;

/// <summary>
/// Lo que hay que escribir para dejar una carga revisada y pendiente de confirmar. Llega ya
/// clasificada: el repositorio persiste, no juzga.
/// </summary>
/// <param name="CampaignId">La campaña, que tiene que seguir en borrador.</param>
/// <param name="OriginalFileName">El nombre del archivo, lo único que se conserva de él.</param>
/// <param name="DetectedEncoding">Con qué codificación se leyó.</param>
/// <param name="RowCount">Filas de datos del archivo, encabezado aparte.</param>
/// <param name="Counts">Las siete cuentas, que suman <paramref name="RowCount"/>.</param>
/// <param name="Rows">Las filas revisadas, en el orden del archivo.</param>
/// <param name="UploadedById">Quién subió.</param>
public sealed record StageRecipientUploadDto(
    Guid CampaignId,
    string OriginalFileName,
    string DetectedEncoding,
    int RowCount,
    UploadBucketCounts Counts,
    IReadOnlyList<StagedUploadRowDto> Rows,
    Guid UploadedById);

/// <summary>Una fila revisada, tal como se guarda.</summary>
/// <param name="SourceRowNumber">La fila del archivo original.</param>
/// <param name="FirstName">Nombre.</param>
/// <param name="LastName">Apellidos.</param>
/// <param name="Email">La dirección, recortada y por lo demás cruda.</param>
/// <param name="Phone">Teléfono.</param>
/// <param name="CompanyRaw">La empresa como texto, sin resolver contra nada.</param>
/// <param name="Bucket">Ver <see cref="UploadRowBucket"/>.</param>
/// <param name="ReasonCode">El motivo, cuando la fila está mal formada o suprimida.</param>
/// <param name="ContactId">El contacto del CRM, si la revisión encontró uno.</param>
/// <param name="DuplicateRowNumbers">Las filas del grupo que comparte correo, si lo hubo.</param>
/// <param name="CollapsedDuplicates">Cuántas filas idénticas se plegaron en esta.</param>
public sealed record StagedUploadRowDto(
    int SourceRowNumber,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? CompanyRaw,
    string Bucket,
    string? ReasonCode,
    Guid? ContactId,
    IReadOnlyList<int> DuplicateRowNumbers,
    int CollapsedDuplicates);

/// <summary>
/// El efecto de una fila confirmada, para escribirlo y marcarla aplicada en una sola
/// transacción. El cubo <b>final</b> decide qué se escribe: destinatario para
/// <c>matched</c> y <c>unmatched</c>, exclusión para <c>suppressed</c>.
/// </summary>
/// <param name="CampaignId">La campaña, que tiene que seguir en borrador.</param>
/// <param name="RowId">La fila del lote.</param>
/// <param name="Bucket">El cubo final, que puede no ser con el que la fila se revisó.</param>
/// <param name="ContactId">El contacto del CRM al que la fila quedó atada.</param>
/// <param name="ReasonCode">El motivo de supresión, cuando el cubo es <c>suppressed</c>.</param>
/// <param name="FirstName">Nombre a copiar en la fila que se escriba.</param>
/// <param name="LastName">Apellidos a copiar.</param>
/// <param name="Email">Dirección a copiar.</param>
public sealed record ApplyUploadRowDto(
    Guid CampaignId,
    Guid RowId,
    string Bucket,
    Guid ContactId,
    string? ReasonCode,
    string FirstName,
    string LastName,
    string? Email);
