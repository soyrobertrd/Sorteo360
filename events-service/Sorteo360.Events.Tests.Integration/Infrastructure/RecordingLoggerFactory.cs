using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Sorteo360.Events.Tests.Integration.Infrastructure;

/// <summary>
/// Una <c>ILoggerFactory</c> que escribe solo en <see cref="RecordingLoggerProvider"/>, para
/// comprobar lo que el servicio deja en el log, y sobre todo lo que <b>no</b> deja.
/// <para>
/// Se sustituye la fábrica entera y no se le engancha un sink a Serilog: Serilog se configura
/// sobre <c>Log.Logger</c>, que es estático y compartido por todas las clases de test, y xUnit las
/// corre en paralelo. Un sink global lo pisaría la siguiente fábrica que se construyera.
/// </para>
/// </summary>
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

/// <summary>Guarda cada línea ya formateada, con su nivel y su categoría.</summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLogLine> _lines = new();

    public IReadOnlyCollection<RecordedLogLine> Lines => _lines;

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(_lines, categoryName);

    public void Dispose() => _lines.Clear();

    private sealed class RecordingLogger : ILogger
    {
        private readonly ConcurrentQueue<RecordedLogLine> _lines;
        private readonly string _category;

        public RecordingLogger(ConcurrentQueue<RecordedLogLine> lines, string category)
        {
            _lines = lines;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>
        /// Todo se registra, hasta <c>Trace</c>. Filtrar por nivel dejaría fuera justamente la
        /// línea de depuración que alguien añade sin pensar y que es la que filtraría un secreto.
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

            _lines.Enqueue(new RecordedLogLine(logLevel, _category, $"{formatter(state, exception)} {exception}"));
        }
    }
}

/// <summary>Una línea del log tal como quedó escrita.</summary>
internal sealed record RecordedLogLine(LogLevel Level, string Category, string Text);
