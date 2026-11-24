using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Notifications.Core.Sending;

namespace Sorteo360.Notifications.Infrastructure.Smtp;

/// <summary>
/// Registra la salida SMTP: la configuración, su freno de arranque y el transporte.
/// </summary>
[ExcludeFromCodeCoverage]
public static class SmtpServiceCollectionExtensions
{
    public static IServiceCollection AddSmtpSending(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // La configuración se valida con FluentValidation, como todo lo que entra al servicio.
        // ValidateOnStart es lo que convierte esto en un freno de verdad: se comprueba al levantar
        // el host, antes de drenar una sola fila, y el servicio no arranca si no pasa. Comprobarlo
        // en el primer envío llegaría tarde y en silencio.
        services.AddSingleton<IValidator<SmtpSettings>, SmtpSettingsValidator>();
        services.AddSingleton<IValidateOptions<SmtpSettings>, SmtpSettingsValidateOptions>();

        services.AddOptions<SmtpSettings>()
            .Bind(configuration.GetSection(SmtpSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<ISmtpSessionFactory, MailKitSmtpSessionFactory>();

        return services;
    }
}
