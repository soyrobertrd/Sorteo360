using System.ComponentModel.DataAnnotations;

namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// Ajustes de la copia en caché del directorio de empresas (<c>CompanyDirectory__*</c>).
/// </summary>
public sealed class CompanyDirectorySettings
{
    public static string SectionName => "CompanyDirectory";

    /// <summary>
    /// Cuántos minutos se considera vigente la copia antes de intentar renovarla. Es un ajuste
    /// propio y no el del directorio de OPT porque las dos copias no envejecen igual: las oficinas
    /// cambian unas pocas veces al año y las empresas se registran a diario.
    /// <para>
    /// Cero es válido y significa renovar en cada lectura; negativo no, y por eso se valida al
    /// arrancar: una vigencia negativa haría que nada se considerara vigente nunca, que es lo mismo
    /// que cero pero por accidente.
    /// </para>
    /// </summary>
    [Range(0, int.MaxValue)]
    public int SnapshotTtlMinutes { get; set; } = 15;

    /// <summary>Antigüedad a partir de la cual toca renovar.</summary>
    public TimeSpan SnapshotTtl => TimeSpan.FromMinutes(SnapshotTtlMinutes);
}
