using Sorteo360.Convocatorias.Core.Rendering;

namespace Sorteo360.Convocatorias.Core.Campaigns;

/// <summary>
/// Los tres valores de grano de evento —<c>{{evento}}</c>, <c>{{lugar}}</c> y <c>{{fecha}}</c>—
/// tal como estaban cuando alguien apretó lanzar, guardados en la propia fila de la campaña.
/// <para>
/// <b>Esto guarda nombres de otro servicio, que la regla de propiedad de la raíz normalmente no
/// admite.</b> Es la misma excepción que ya se toma <c>campaign_recipient</c> con el nombre y la
/// dirección que vienen del CRM: una copia en el camino de envío no es una consulta de
/// presentación, porque un correo tiene que seguir diciendo lo que dijo. Reanudar una campaña una
/// semana después no puede mandar un texto distinto del que ya salió en el primer correo.
/// </para>
/// <para>
/// Las otras dos opciones pierden lo que hace falta para reanudar. Llevarlos en el canal en
/// memoria no sobrevive al redespliegue que dejó la campaña a medias, que es justo cuando se
/// reanuda; releer events-service desde el worker convierte una caída del vecino en una campaña
/// atascada en <c>sending</c>.
/// </para>
/// </summary>
/// <param name="Name">El nombre del evento.</param>
/// <param name="Location">Las ciudades del recorrido, unidas.</param>
/// <param name="Dates">El día o el tramo.</param>
public sealed record CampaignEventValues(string Name, string Location, string Dates)
{
    /// <summary>
    /// Los toma del mapa que arma <c>EventPlaceholderValues</c>, que es quien sabe darles formato.
    /// Una clave ausente sale vacía, igual que un valor vacío: para quien comprueba significan lo
    /// mismo, y el lanzamiento ya rechazó antes de llegar acá si faltaba alguno.
    /// </summary>
    public static CampaignEventValues From(IReadOnlyDictionary<string, string> placeholderValues)
    {
        ArgumentNullException.ThrowIfNull(placeholderValues);

        return new CampaignEventValues(
            placeholderValues.GetValueOrDefault(SupportedPlaceholder.EventName, string.Empty),
            placeholderValues.GetValueOrDefault(SupportedPlaceholder.EventLocation, string.Empty),
            placeholderValues.GetValueOrDefault(SupportedPlaceholder.EventDates, string.Empty));
    }

    /// <summary>
    /// Los mismos tres, de vuelta como bolsa compartida del lote (spec <c>04</c> §2).
    /// <b>Compartida y no repetida</b>: es lo que mantiene el nombre del evento fuera del cable mil
    /// veces, y es también por qué una clave que esté acá no puede estar además en la bolsa de una
    /// persona.
    /// </summary>
    public IReadOnlyDictionary<string, string> ToSharedData() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SupportedPlaceholder.EventName]     = Name,
            [SupportedPlaceholder.EventLocation] = Location,
            [SupportedPlaceholder.EventDates]    = Dates,
        };
}
