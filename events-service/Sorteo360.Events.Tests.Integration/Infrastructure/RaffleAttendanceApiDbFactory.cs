using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sorteo360.Events.Core.Raffles;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// La misma aplicación de siempre, con la puerta en manos del test (<see cref="FakeAttendanceSource"/>)
/// y la copia de la puerta vigente cero segundos: cada lectura vuelve a preguntar, así que una
/// caída programada se nota en la lectura siguiente sin mover ningún reloj. La lista de DO Travel
/// Awards también es del test (<see cref="FakeAwardsMembership"/>), y empieza sin ninguna en vigor,
/// como el marcador registrado.
/// </summary>
public class RaffleAttendanceApiDbFactory : EventsApiDbFactory
{
    /// <summary>La puerta que resuelve la aplicación.</summary>
    public FakeAttendanceSource Attendance { get; } = new();

    /// <summary>La lista de DO Travel Awards que resuelve la aplicación.</summary>
    public FakeAwardsMembership Awards { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("RaffleAttendance:CacheTtlSeconds", "0");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAttendanceSource>(Attendance);
            services.AddSingleton<IAwardsMembership>(Awards);
        });
    }
}
