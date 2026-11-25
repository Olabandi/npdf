using Avalonia;

namespace NeroPdf.MacOS;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Wird auch vom Avalonia-Designer in Visual Studio verwendet.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<NeroPdf.App.App>()
            .UsePlatformDetect();
}
