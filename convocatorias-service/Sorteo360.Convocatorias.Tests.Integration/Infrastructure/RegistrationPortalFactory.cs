using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sorteo360.Convocatorias.Tests.Integration.Infrastructure;

/// <summary>
/// La fábrica del portal de registro. Es la de siempre más un registro de lo que el servicio
/// escribe en el log, porque una de las condiciones de la spec <c>06</c> es una <b>ausencia</b>:
/// ningún token puede aparecer en una línea de log (§2.1, definición de hecho).
/// <para>
/// Se sustituye la <c>ILoggerFactory</c> entera y no se le engancha un sink a Serilog: Serilog
/// se configura sobre <c>Log.Logger</c>, que es estático y compartido por todas las clases de
/// test, y xUnit las corre en paralelo. Un sink global lo pisaría la siguiente fábrica que se
/// construyera, y el test pasaría sin haber mirado nada.
/// </para>
/// </summary>
public sealed class RegistrationPortalFactory : ConvocatoriasApiDbFactory
{
    private readonly RecordingLoggerProvider _recorder = new();

    /// <summary>Todo lo que el servicio escribió, en orden de llegada.</summary>
    public IReadOnlyCollection<string> LoggedLines => _recorder.Lines;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Va la última, que es la que gana al resolver: `UseSerilog` registró la suya antes.
        builder.ConfigureTestServices(services =>
            services.AddSingleton<ILoggerFactory>(new RecordingLoggerFactory(_recorder)));
    }
}

/// <summary>Una <c>ILoggerFactory</c> que solo sabe escribir en el registro de arriba.</summary>
internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    private readonly RecordingLoggerProvider _provider;

    public RecordingLoggerFactory(RecordingLoggerProvider provider) => _provider = provider;

    public ILogger CreateLogger(string categoryName) => _provider.CreateLogger(categoryName);

    public void AddProvider(ILoggerProvider provider)
    {
        // No hay más proveedores que el que recoge: es una fábrica de test, no una tubería.
    }

    public void Dispose() => _provider.Dispose();
}

/// <summary>Guarda el mensaje ya formateado de cada línea, que es lo que se inspecciona.</summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<string> Lines => _lines;

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(_lines, categoryName);

    public void Dispose() => _lines.Clear();

    private sealed class RecordingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _lines;
        private readonly string _category;

        public RecordingLogger(ConcurrentQueue<string> lines, string category)
        {
            _lines = lines;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>
        /// Todo se registra, hasta <c>Trace</c>. Filtrar por nivel dejaría fuera justamente la
        /// línea de depuración que alguien añade sin pensar y que es la que filtraría el token.
        /// </summary>
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            _lines.Enqueue($"[{logLevel}] {_category}: {formatter(state, exception)} {exception}");
        }
    }
}
