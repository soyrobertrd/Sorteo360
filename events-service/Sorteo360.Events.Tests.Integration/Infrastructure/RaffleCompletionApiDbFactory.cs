using Microsoft.AspNetCore.Hosting;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// La aplicación del sorteo con todo lo que toca completarlo: la puerta en manos del test, el
/// almacenamiento que se puede tirar abajo y un realm que contesta, del que salen los nombres de los
/// operadores que el acta escribe.
/// </summary>
public sealed class RaffleCompletionApiDbFactory : RaffleFileStoreOutageApiDbFactory
{
    /// <summary>El realm simulado del que salen los nombres de los operadores.</summary>
    public KeycloakRealmStub Realm { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Después de la base, para pisar el http://keycloak.invalid que deja ServiceIdentityStub.
        builder.UseSetting("Services:Keycloak:BaseUrl", Realm.Url);
        builder.UseSetting("QuoteApprovers:Realm", "sorteo360");
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Realm.Dispose();

        base.Dispose(disposing);
    }
}
