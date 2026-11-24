using System.Linq.Expressions;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// Qué campaña cuenta en las lecturas del evento (spec <c>07</c> §2): la que se lanzó. No "la que
/// no es borrador": una cancelada desde borrador nunca salió, y sus filas <c>pending</c> se
/// sentarían en <c>pendingSend</c> al lado de las varadas de un envío cortado, que dicen lo
/// contrario.
/// <para>
/// Es la única definición: la unión de participantes y la lista de campañas del resumen la usan
/// las dos, así que no se pueden separar.
/// </para>
/// </summary>
internal static class LaunchedCampaign
{
    public static readonly Expression<Func<CampaignData, bool>> Is = campaign => campaign.LaunchedAt != null;
}
