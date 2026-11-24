using System.ComponentModel.DataAnnotations;

namespace Sorteo360.Events.Infrastructure.Raffles;

/// <summary>
/// Ajustes de la copia en caché de la lista de la puerta de un sorteo (<c>RaffleAttendance__*</c>).
/// </summary>
public sealed class RaffleAttendanceSettings
{
    public static string SectionName => "RaffleAttendance";

    /// <summary>
    /// Cuántos segundos se considera vigente la copia antes de volver a leer la puerta. Es corto
    /// porque la gente sigue llegando, y solo las llegadas pueden atrasarse: lo demás se aplica en
    /// cada lectura.
    /// <para>
    /// Cero es válido y significa leer en cada consulta; negativo no, y por eso se valida al
    /// arrancar.
    /// </para>
    /// </summary>
    [Range(0, int.MaxValue)]
    public int CacheTtlSeconds { get; set; } = 30;

    /// <summary>Antigüedad a partir de la cual toca volver a leer.</summary>
    public TimeSpan CacheTtl => TimeSpan.FromSeconds(CacheTtlSeconds);
}
