using NeroPdf.Core.Platform;
using Windows.Storage;

namespace NeroPdf.Windows;

/// <summary>
/// Windows implementations. Installed as MSIX package, folders come from the package;
/// started directly from the build output (development), the user's local app data folder is used
/// and there is no virtual printer.
/// macOS counterpart: NeroPdf.MacOS (Application Support folder, PDF service in ~/Library/PDF Services).
/// </summary>
internal static class WindowsPlatform
{
    // Width of the three Windows caption buttons (3 × 46 px) that sit inside the header.
    private const double CaptionButtonsWidth = 138;

    public static PlatformServices Create()
    {
        var packaged = PackageInfo.IsPackaged;
        var data = packaged
            ? ApplicationData.Current.LocalFolder.Path
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeroPdf");
        IPrintInbox inbox = packaged
            ? new FolderPrintInbox(ApplicationData.Current.TemporaryFolder.Path)
            : new NoPrintInbox();
        IDefaultPdfApp defaultApp = packaged ? new WindowsDefaultPdfApp() : new UnsupportedDefaultPdfApp();
        IFileConverter converter = packaged ? new WindowsFileConverter() : new UnsupportedExtras();
        return new PlatformServices(new WindowsPaths(data), inbox, new HeaderInsets(0, CaptionButtonsWidth), defaultApp,
            new WindowsNeroSuite(), converter, new WindowsScanner());
    }

    private sealed record WindowsPaths(string DataDirectory) : IPlatformPaths;

}

internal static class PackageInfo
{
    public static bool IsPackaged { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            return global::Windows.ApplicationModel.Package.Current is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
