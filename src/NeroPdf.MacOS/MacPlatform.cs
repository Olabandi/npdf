using NeroPdf.Core.Platform;

namespace NeroPdf.MacOS;

/// <summary>
/// macOS implementations. The PDF service in the print dialog (counterpart of the Windows virtual
/// printer) follows with the Mac build; until then there is no print inbox.
/// </summary>
internal static class MacPlatform
{
    // Space for the three macOS window buttons at the left of the header.
    private const double WindowButtonsWidth = 72;

    public static PlatformServices Create()
    {
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "NeroPdf");
        // Default app on macOS (LSSetDefaultRoleHandlerForContentType) follows with the Mac build.
        return new PlatformServices(new MacPaths(data), new NoPrintInbox(), new HeaderInsets(WindowButtonsWidth, 0),
            new UnsupportedDefaultPdfApp());
    }

    private sealed record MacPaths(string DataDirectory) : IPlatformPaths;

    private sealed class NoPrintInbox : IPrintInbox
    {
        public string? TryClaimNext() => null;

        public void CleanUp(TimeSpan maxAge)
        {
        }
    }
}
