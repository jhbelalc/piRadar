#if DEBUG
using Microsoft.Extensions.Logging;

namespace PiRadar.App.Services;

/// <summary>
/// SOLO DEBUG: advertencias y errores a <c>AppDataDirectory/app-log.txt</c>. El proveedor Debug no llega a logcat
/// ni a la consola del simulador, y sin esto las excepciones de Blazor quedan invisibles.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private static readonly Lock Gate = new();
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "app-log.txt");

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(string line)
    {
        lock (Gate)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write($"{DateTime.Now:HH:mm:ss} {logLevel} {category}: {formatter(state, exception)} {exception}");
            }
        }
    }
}
#endif
