namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Lo que dejó una selección: cuántos contactos trajo el CRM, cuántos se materializaron como
/// destinatarios y cuántos quedaron excluidos, con el desglose por motivo.
/// <para>
/// Las cuentas cuadran a propósito —<c>Matched = Added + Excluded + Skipped</c>— porque es
/// exactamente la pregunta que se hace quien esperaba 240 y ve 227. El desglose contesta la
/// segunda mitad, "¿por qué?", sin obligar a abrir la lista de exclusiones.
/// </para>
/// </summary>
/// <param name="Matched">Contactos que devolvió el CRM para ese filtro.</param>
/// <param name="Added">Destinatarios nuevos que escribió esta selección.</param>
/// <param name="Excluded">Exclusiones nuevas que escribió esta selección.</param>
/// <param name="Skipped">
/// Contactos que ya estaban en la campaña, como destinatario o como exclusión, y que esta
/// selección no volvió a escribir. Es lo que hace que seleccionar dos veces componga en vez de
/// duplicar.
/// </param>
/// <param name="ExcludedByReason">Cuántas exclusiones nuevas por cada código de motivo.</param>
public sealed record RecipientSelection(
    int Matched,
    int Added,
    int Excluded,
    int Skipped,
    IReadOnlyDictionary<string, int> ExcludedByReason);
