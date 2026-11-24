using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sorteo360.Notifications.Core.Sending;

namespace Sorteo360.Notifications.Tests.Integration.Infrastructure;

/// <summary>
/// La API contra un PostgreSQL de verdad y con la pata SMTP simulada, para ejercitar las reglas del
/// worker: el reintento, la supresión, la hora, y el reparto entre dos drenadores.
/// <para>
/// El <c>BackgroundService</c> no corre en "IntegrationTesting": el test llama al servicio de
/// drenaje él mismo. Es a propósito —así el test comprueba un resultado en vez de dormir esperando
/// una vuelta— y es lo que hace que la prueba de concurrencia pueda lanzar dos drenadores a la vez
/// a voluntad.
/// </para>
/// </summary>
public sealed class NotificationsDrainFactory : NotificationsApiDbFactory
{
    /// <summary>La pata SMTP simulada que ve el worker.</summary>
    public FakeSmtpSessionFactory Smtp { get; } = new();

    /// <summary>Un drenador nuevo, con su propio ámbito y su propio <c>DbContext</c>.</summary>
    public async Task<int> DrainAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var drain = scope.ServiceProvider.GetRequiredService<INotificationDrainService>();

        return await drain.DrainAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Mailpit no puede producir un 535 y no puede rechazar nada: las reglas se prueban contra
        // el doble, los bytes contra Mailpit.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmtpSessionFactory>();
            services.AddSingleton<ISmtpSessionFactory>(Smtp);
        });
    }
}
