using FluentValidation;

namespace Sorteo360.Notifications.Core.Sending;

/// <summary>
/// La configuración de salida SMTP, comprobada al arrancar. Hoy lleva una sola regla y es el freno
/// de <see cref="SmtpRelayGuard"/>: la regla vive allá, en una función pura que se puede probar
/// sola, y acá solo se declara cuándo se aplica.
/// </summary>
public sealed class SmtpSettingsValidator : AbstractValidator<SmtpSettings>
{
    public SmtpSettingsValidator()
    {
        // Una sola regla, que es la que la especificación pide. Exigir acá un host o un puerto
        // sería inventar política: los tres compose ya los traen, y un servicio que se negara a
        // arrancar por eso rompería cualquier host que no vaya a mandar nada.
        RuleFor(settings => settings)
            .Must(SmtpRelayGuard.IsAllowed)
            .WithMessage(SmtpRelayGuard.RefusalMessage);
    }
}
