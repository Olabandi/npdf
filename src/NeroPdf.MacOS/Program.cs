using Avalonia;

namespace NeroPdf.MacOS;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => NeroPdf.App.AppHost.Run(MacPlatform.Create(), args, BuildAvaloniaApp);

    // Also used by the Avalonia designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<NeroPdf.App.App>()
            .UsePlatformDetect();
}
