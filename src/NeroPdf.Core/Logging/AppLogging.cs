using Microsoft.Extensions.Logging;

namespace NeroPdf.Core.Logging;

/// <summary>Creates the central logger factory. Verbose logging only in Debug builds or via --verbose.</summary>
public static class AppLogging
{
    public const string VerboseArgument = "--verbose";

    public static ILoggerFactory Create(string logDirectory, bool verbose) =>
        LoggerFactory.Create(builder => builder
            .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information)
            .AddProvider(new FileLoggerProvider(logDirectory)));

    public static bool IsVerbose(IEnumerable<string> args)
    {
#if DEBUG
        return true;
#else
        return args.Contains(VerboseArgument, StringComparer.OrdinalIgnoreCase);
#endif
    }
}
