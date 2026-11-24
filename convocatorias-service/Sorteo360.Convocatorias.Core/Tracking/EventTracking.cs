using Sorteo360.Convocatorias.Core.Campaigns;
using Sorteo360.Convocatorias.Core.Registrations;

namespace Sorteo360.Convocatorias.Core.Tracking;

/// <summary>El resumen de un evento, tal como lo sirve la ruta (spec <c>07</c> §2).</summary>
/// <param name="Campaigns">Las campañas del evento que no son borrador, con sus dos cuentas.</param>
/// <param name="Rollup">Personas, inscripciones y quién falta, de la lista de participantes.</param>
/// <param name="NeedsAttention">Lo que una persona tiene que ir a arreglar en este evento.</param>
/// <param name="RegistrationWindow">La puerta de inscripción. No es un conteo ni pide atención.</param>
public sealed record EventTracking(
    IReadOnlyList<CampaignSummary> Campaigns,
    TrackingRollup Rollup,
    int NeedsAttention,
    RegistrationWindow RegistrationWindow);
