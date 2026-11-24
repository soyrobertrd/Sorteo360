using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Errors;
using Sorteo360.Platform.Errors;

namespace Sorteo360.Convocatorias.Infrastructure.Database;

/// <summary>
/// "Esta campaña sigue siendo un borrador", en un solo sitio. La piden la edición, la selección,
/// el borrado de un destinatario y las dos mitades de una carga de lista, y desde que se retiró
/// la aprobación es la <b>regla entera</b> de qué se puede tocar: <c>sending</c>, <c>sent</c> y
/// <c>cancelled</c> son de solo lectura (spec <c>03</c> §5, spec <c>05</c> §1).
/// <para>
/// Vive acá y no en un endpoint a propósito: se comprueba <b>dentro</b> de la transacción que
/// escribe. Leer el estado antes, y decidir después, dejaría pasar dos escrituras simultáneas
/// contra un borrador que dejó de serlo mientras la petición estaba en vuelo.
/// </para>
/// <para>
/// La fila que devuelve viene <b>rastreada</b>, porque quien la pide suele sellarle
/// <c>updated_at</c> en la misma transacción.
/// </para>
/// </summary>
internal static class CampaignDraftGuard
{
    /// <summary>
    /// Devuelve o la campaña, o el rechazo ya armado que hay que devolver tal cual: nada si ese
    /// id no existe, y <c>CAMPAIGN_NOT_DRAFT</c> con el estado en el que sí está si ya no admite
    /// cambios.
    /// </summary>
    /// <typeparam name="T">Lo que devuelve la operación que pide el borrador.</typeparam>
    public static async Task<(CampaignData? Campaign, Result<Maybe<T>, BaseError> Refusal)> LoadAsync<T>(
        ConvocatoriasDbContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        var campaign = await context.Campaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (campaign is null)
            return (null, Result.Success<Maybe<T>, BaseError>(Maybe<T>.None));

        if (campaign.State != CampaignState.Draft)
            return (null, Result.Failure<Maybe<T>, BaseError>(new CampaignNotDraftError(campaign.State)));

        return (campaign, default);
    }
}
