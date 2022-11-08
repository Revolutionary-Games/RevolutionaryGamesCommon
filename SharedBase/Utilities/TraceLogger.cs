namespace SharedBase.Utilities;

using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

/// <summary>
///   Logger that puts everything to trace
/// </summary>
public class TraceLogger<TCategoryName> : ILogger<TCategoryName>
{
    private readonly string prefix = typeof(TCategoryName).Name;

    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        Trace.WriteLine($"[{prefix}][{logLevel}] {formatter(state, exception)}");
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return default;
    }
}
