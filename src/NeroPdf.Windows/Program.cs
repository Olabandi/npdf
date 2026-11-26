using Avalonia;

namespace NeroPdf.Windows;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) =>
        NeroPdf.App.AppHost.Run(WindowsPlatform.Create(), WithoutActivationArguments(args), BuildAvaloniaApp);

    // Also used by the Avalonia designer in Visual Studio.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<NeroPdf.App.App>()
            .UseWin32()
            .UseSkia()
            .UseHarfBuzz();

    // When the print task starts Nero PDF, Windows adds "/InvokerPRAID: App"; that is not a file to open.
    private static string[] WithoutActivationArguments(string[] args)
    {
        var index = Array.FindIndex(args, a => a.StartsWith("/InvokerPRAID", StringComparison.OrdinalIgnoreCase));
        return index < 0 ? args : args[..index];
    }
}
