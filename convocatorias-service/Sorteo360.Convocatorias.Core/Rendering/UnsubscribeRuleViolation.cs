namespace Sorteo360.Convocatorias.Core.Rendering;

/// <summary>
/// Cómo incumple una plantilla la regla del enlace de baja, que se invierte según el propósito
/// (spec <c>02</c> §2.1).
/// </summary>
public enum UnsubscribeRuleViolation
{
    /// <summary>La plantilla cumple.</summary>
    None,

    /// <summary>
    /// Una plantilla de campaña sin <c>{{enlace_baja}}</c>. Es el fallo que la regla existe para
    /// atrapar: un correo de campaña que no puede llevar enlace de baja.
    /// </summary>
    MissingOnCampaign,

    /// <summary>
    /// Una plantilla transaccional con <c>{{enlace_baja}}</c>. Contesta al propio acto de la
    /// persona, así que no hay nada de qué darse de baja, y ofrecerlo sería mentir.
    /// </summary>
    PresentOnTransactional
}
