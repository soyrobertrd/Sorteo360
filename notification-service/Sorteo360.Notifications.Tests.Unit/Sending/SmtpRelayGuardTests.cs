using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sorteo360.Notifications.Core.Sending;
using Sorteo360.Notifications.Infrastructure.Smtp;
using Shouldly;

namespace Sorteo360.Notifications.Tests.Unit.Sending;

/// <summary>
/// El freno de arranque. Se ejercita por el mismo registro que usa <c>Program</c>, no por la
/// función suelta: lo que hay que probar no es que la regla sepa decir "no", es que el host no
/// levante, y eso lo decide <c>ValidateOnStart</c>.
/// </summary>
public sealed class SmtpRelayGuardTests
{
    [Fact]
    public void Validate_ThrowsOptionsValidationException_WhenAPasswordIsSetWithoutPermissionToRelay()
    {
        var validator = Validator(allowExternalRelay: false, password: "SG.una-api-key-de-verdad");

        var failure = Should.Throw<OptionsValidationException>(validator.Validate);

        failure.Message.ShouldContain("Smtp__AllowExternalRelay");
    }

    [Fact]
    public void Validate_ReturnsSuccess_WhenThereIsNoPasswordAndNoPermissionToRelay()
    {
        // Es la configuración local y la de stage: Mailpit no pide credenciales, así que no hay
        // ninguna forma física de que el correo salga a internet.
        var validator = Validator(allowExternalRelay: false, password: "");

        Should.NotThrow(validator.Validate);
    }

    [Fact]
    public void Validate_ReturnsSuccess_WhenAPasswordIsSetAndRelayingIsAllowed()
    {
        // Producción, y el único envío real de prueba desde stage el día que IT abra el puerto.
        var validator = Validator(allowExternalRelay: true, password: "SG.una-api-key-de-verdad");

        Should.NotThrow(validator.Validate);
    }

    [Fact]
    public void IsAllowed_ReturnsTrue_WhenThePasswordIsBlankSpace()
    {
        SmtpRelayGuard.IsAllowed(new SmtpSettings { AllowExternalRelay = false, Password = "   " })
            .ShouldBeTrue();
    }

    /// <summary>Construye el contenedor como lo construye el servicio y saca su validador.</summary>
    private static IStartupValidator Validator(bool allowExternalRelay, string password)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "mailpit",
                ["Smtp:Port"] = "1025",
                ["Smtp:RequireTls"] = "false",
                ["Smtp:Password"] = password,
                ["Smtp:AllowExternalRelay"] = allowExternalRelay.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSmtpSending(configuration);

        return services.BuildServiceProvider().GetRequiredService<IStartupValidator>();
    }
}
