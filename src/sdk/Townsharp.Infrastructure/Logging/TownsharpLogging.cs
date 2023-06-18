using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Townsharp.Infrastructure.Logging;

public static class TownsharpLogging
{
    public static ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

    public static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
}

