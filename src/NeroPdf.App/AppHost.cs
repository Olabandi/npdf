using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Infrastructure;
using NeroPdf.Core.Localization;
using NeroPdf.Core.Logging;
using NeroPdf.Core.Platform;

namespace NeroPdf.App;

/// <summary>Start of the shared application. Called by the platform projects.</summary>
public static class AppHost
{
    internal static Stopwatch StartTime { get; } = new();

    internal static PlatformServices? Platform { get; private set; }

    internal static ILoggerFactory LoggerFactory { get; private set; } =
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

    /// <summary>File given on the command line, if any.</summary>
    internal static string? FileArgument { get; private set; }

    /// <summary>"/forcefree": behave as if no Nero suite were installed (to try the free variant).</summary>
    internal static bool ForceFree { get; private set; }

    private const string ForceFreeArgument = "/forcefree";

    public static int Run(PlatformServices platform, string[] args, Func<AppBuilder> buildApp)
    {
        StartTime.Start();
        Platform = platform;
        LoggerFactory = AppLogging.Create(platform.Paths.LogDirectory, AppLogging.IsVerbose(args));
        var logger = LoggerFactory.CreateLogger(typeof(AppHost));
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception");
        logger.LogInformation("Nero PDF {Version} starting on {Os}, culture {Culture}",
            typeof(AppHost).Assembly.GetName().Version, Environment.OSVersion, CultureInfo.CurrentUICulture.Name);

        Texts.Current = Localizer.Load(Path.Combine(AppContext.BaseDirectory, "Languages"),
            CultureInfo.CurrentUICulture, LoggerFactory.CreateLogger<Localizer>());
        ForceFree = args.Contains(ForceFreeArgument, StringComparer.OrdinalIgnoreCase);
        if (ForceFree)
        {
            logger.LogInformation("Started with {Argument}: scan and convert stay locked", ForceFreeArgument);
        }
        FileArgument = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)
            && !a.Equals(ForceFreeArgument, StringComparison.OrdinalIgnoreCase));

        try
        {
            return buildApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            logger.LogInformation("Nero PDF closed");
            LoggerFactory.Dispose();
        }
    }
}
