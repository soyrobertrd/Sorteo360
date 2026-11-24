using Microsoft.Extensions.Logging;

namespace Sorteo360.Convocatorias.Tests.Unit.Neighbours;

/// <summary>
/// Un <see cref="ILogger{TCategoryName}"/> que se queda con lo que le escriben. Existe para las
/// pruebas que no miran el resultado sino el registro: que un visitante que se va no quede
/// anotado como una caída del vecino solo se puede comprobar mirando lo que <b>no</b> se escribió.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

    public IReadOnlyList<string> Errors =>
        _entries.Where(entry => entry.Level >= LogLevel.Error).Select(entry => entry.Message).ToList();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _entries.Add((logLevel, formatter(state, exception)));
}
