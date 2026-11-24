using Sorteo360.Events.Core.CrmDirectory;
using Sorteo360.Events.Infrastructure.Directories;

namespace Sorteo360.Events.Infrastructure.Crm;

/// <summary>
/// Traduce el resultado de una lectura en caché al vocabulario de degradación del CRM.
/// <para>
/// Vive acá y no en <see cref="StaleTolerantCache"/> porque la máquina no conoce ningún origen, y
/// es una sola para los dos directorios del CRM porque los dos hablan del mismo CRM: una escalera
/// de degradación escrita en dos sitios puede terminar contradiciéndose, y el estado es uno por
/// petición para todo lo que se resuelve contra el CRM (ADR-0015).
/// </para>
/// </summary>
internal static class CacheOutcomeExtensions
{
    /// <summary>
    /// Una copia vigente o recién renovada es <see cref="CrmDirectoryStatus.Ok"/>; una vencida que
    /// se sirvió igual es <see cref="CrmDirectoryStatus.Stale"/>; no tener ninguna es
    /// <see cref="CrmDirectoryStatus.Unavailable"/>.
    /// </summary>
    public static CrmDirectoryStatus ToCrmDirectoryStatus(this CacheOutcome outcome) =>
        outcome switch
        {
            CacheOutcome.Fresh => CrmDirectoryStatus.Ok,
            CacheOutcome.Stale => CrmDirectoryStatus.Stale,
            _ => CrmDirectoryStatus.Unavailable,
        };
}
