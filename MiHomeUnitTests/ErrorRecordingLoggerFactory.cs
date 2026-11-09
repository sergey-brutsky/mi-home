using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MiHomeUnitTests;

/// <summary>
/// Keeps the exceptions the library logs instead of throwing, for example a failed assertion inside
/// a device event handler. Test bases fail a test that leaves any of them unchecked.
/// Every log entry is kept as well, for tests of the logging itself
/// </summary>
public sealed class ErrorRecordingLoggerFactory : ILoggerFactory
{
    private readonly ConcurrentQueue<Exception> _errors = new();
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public record LogEntry(string Category, LogLevel Level, string Message);
    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public List<Exception> TakeErrors()
    {
        var errors = new List<Exception>();
        while (_errors.TryDequeue(out var e)) errors.Add(e);
        return errors;
    }

    public void ThrowIfAnyErrors()
    {
        var errors = TakeErrors();
        if (errors.Count > 0) throw new AggregateException("The library logged exceptions it swallowed", errors);
    }

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _errors, _entries);
    public void AddProvider(ILoggerProvider provider) { }
    public void Dispose() { }

    private sealed class Logger(string category, ConcurrentQueue<Exception> errors, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception)));
            if (exception is not null) errors.Enqueue(exception);
        }
    }
}
