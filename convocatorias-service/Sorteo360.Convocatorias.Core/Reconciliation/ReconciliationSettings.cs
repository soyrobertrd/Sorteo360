using System.ComponentModel.DataAnnotations;

namespace Sorteo360.Convocatorias.Core.Reconciliation;

/// <summary>
/// Ajustes de la pantalla de reconciliación. Sección <c>Reconciliation</c>.
/// </summary>
public sealed record ReconciliationSettings
{
    public static string SectionName => "Reconciliation";

    /// <summary>
    /// Cuánto tiempo en <c>sending</c> con filas <c>pending</c> hace falta para llamar varada a una
    /// campaña (spec <c>07</c> §6.3). <b>Se mide contra nuestra entrega a notification-service, no
    /// contra la llegada del correo</b>: una campaña entera <c>handed_off</c> no está varada aunque
    /// el correo siga saliendo. Un valor corto solo cuesta un falso positivo que se va al refrescar.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan StrandedCampaignAfter { get; init; } = TimeSpan.FromMinutes(15);
}
