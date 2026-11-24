namespace Sorteo360.Convocatorias.Core.Uploads;

/// <summary>
/// Una fila después de las dos reglas que se deciden <b>sin salir del archivo</b>: si está mal
/// formada y si choca con otra fila del mismo archivo (spec <c>05</c> §4 cubos 1 y 2).
/// <para>
/// <see cref="Bucket"/> nulo significa "hasta acá llega lo que se puede decidir solo": esa fila
/// necesita una lectura del CRM para saber si está, si ya es destinataria o si está suprimida.
/// </para>
/// </summary>
/// <param name="Row">La fila leída.</param>
/// <param name="Bucket">El cubo ya decidido, o <c>null</c> si falta preguntarle al CRM.</param>
/// <param name="ReasonCode">El motivo, cuando el cubo es <c>malformed</c>.</param>
/// <param name="NormalizedEmail">
/// La clave local de deduplicación: recorte y minúsculas y <b>nada más</b>. Es a propósito un
/// subconjunto conservador de lo que el CRM pueda hacer, así que cualquier diferencia entre los
/// dos nos hace unir <i>de menos</i>, que sale como un 409 —un desenlace ya previsto y que no es
/// un fallo— en vez de unir a dos personas distintas en silencio (§5).
/// </param>
/// <param name="DuplicateRowNumbers">
/// Los números de fila del grupo que comparte correo, cuando el cubo es <c>duplicate_in_file</c>.
/// El cubo los nombra todos para que quien subió pueda ir a mirarlos. Vacío en cualquier otro caso.
/// </param>
/// <param name="CollapsedDuplicates">
/// Cuántas filas idénticas se plegaron en esta. Nadie tiene que elegir entre dos filas iguales,
/// así que se cuenta y se sigue.
/// </param>
public sealed record ClassifiedUploadRow(
    ParsedUploadRow Row,
    string? Bucket,
    string? ReasonCode,
    string? NormalizedEmail,
    IReadOnlyList<int> DuplicateRowNumbers,
    int CollapsedDuplicates);
