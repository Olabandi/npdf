using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NeroPdf.Core.Platform;

namespace NeroPdf.Windows;

/// <summary>
/// Default PDF app on Windows 11. Programs may not make themselves the default; the check uses the
/// current association of ".pdf", and the settings page of Nero PDF offers "Set default" to the user.
/// The file type association itself (for "Open with") is declared in the package manifest.
/// macOS counterpart: LSSetDefaultRoleHandlerForContentType, with the Mac build.
/// </summary>
internal sealed class WindowsDefaultPdfApp : IDefaultPdfApp
{
    private const uint AssocStringAppId = 21;
    private const string ApplicationId = "App";

    private readonly string _appUserModelId =
        global::Windows.ApplicationModel.Package.Current.Id.FamilyName + "!" + ApplicationId;

    public bool IsSupported => true;

    public bool IsDefault()
    {
        uint length = 512;
        var buffer = new StringBuilder((int)length);
        var result = AssocQueryString(0, AssocStringAppId, ".pdf", null, buffer, ref length);
        return result == 0 && string.Equals(buffer.ToString(), _appUserModelId, StringComparison.OrdinalIgnoreCase);
    }

    public void OpenSettings() =>
        Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAUMID=" + Uri.EscapeDataString(_appUserModelId))
        {
            UseShellExecute = true,
        });

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags, uint str, string assoc, string? extra, StringBuilder? output, ref uint length);
}
