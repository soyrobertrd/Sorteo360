using Microsoft.AspNetCore.Hosting;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// La API contra un PostgreSQL de verdad y un Mailpit de verdad. Es el único sitio del conjunto de
/// pruebas donde el transporte es el real —MailKit, la conexión, el <c>DATA</c>, el armado MIME—,
/// y es lo que hace que el camino de producción no se ejecute por primera vez en producción.
/// </summary>
public sealed class MailpitApiFactory : NotificationsApiDbFactory
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;

    public MailpitApiFactory(string smtpHost, int smtpPort)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("Smtp:Host", _smtpHost);
        builder.UseSetting("Smtp:Port", _smtpPort.ToString());

        // La ÚNICA línea que difiere entre este camino y el de producción. Mailpit sirve texto
        // plano; el proveedor exige STARTTLS. Todo lo demás —el cliente, las cabeceras, las dos
        // partes MIME, una transacción por persona— es el mismo código.
        builder.UseSetting("Smtp:RequireTls", "false");

        // Sin credencial y sin permiso de salida: la configuración que el freno de arranque deja
        // pasar, y la que hace imposible que esto mande correo a internet.
        builder.UseSetting("Smtp:AllowExternalRelay", "false");
        builder.UseSetting("Smtp:Password", string.Empty);
    }
}
