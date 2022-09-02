namespace TestUtilities.Utilities;

using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public sealed class XunitLogger<T> : ILogger<T>, IDisposable
{
    private readonly ITestOutputHelper output;

    public XunitLogger(ITestOutputHelper output)
    {
        this.output = output;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        output.WriteLine(formatter(state, exception));
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return this;
    }

    public void Dispose()
    {
    }
}
