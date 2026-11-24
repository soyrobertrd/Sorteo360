namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Una campaña con las dos cuentas que toda pantalla que la muestre necesita: cuántos
/// destinatarios lleva y cuántos quedaron excluidos.
/// <para>
/// Las cuentas se calculan al leer y no viven en columnas de <c>campaign</c>: una columna
/// contadora tiene que actualizarla cada escritura de destinatario, y el día que una se olvide
/// la fila miente sin que nada lo note. Son dos <c>COUNT</c> sobre tablas indexadas por
/// campaña.
/// </para>
/// </summary>
/// <param name="Campaign">La campaña.</param>
/// <param name="RecipientCount">Filas de <c>campaign_recipient</c>.</param>
/// <param name="ExclusionCount">Filas de <c>campaign_exclusion</c>.</param>
public sealed record CampaignSummary(
    Campaign Campaign,
    int RecipientCount,
    int ExclusionCount);
