using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using NeroPdf.Core.Platform;

namespace NeroPdf.Windows;

/// <summary>
/// Converts files to PDF on Windows:
/// - Images (BMP, PNG, GIF, JPEG, TIFF) directly: WIA turns every image or frame into a JPEG page.
///   Windows 11 has no print command for them that accepts a printer.
/// - Text files with Notepad, which prints to a given printer with "/pt".
/// - Everything else with the "printto" command of the program registered for the type (e.g. Word),
///   with the Nero PDF printer as target. The result arrives in the print inbox like any print job.
/// macOS counterpart: none yet; converting stays locked there.
/// </summary>
internal sealed class WindowsFileConverter : IFileConverter
{
    private const uint AssocStringCommand = 1;
    private const string PrintToVerb = "printto";

    // Images become pages of their own size, but not larger than A4 (in points, either orientation).
    private const double MaxPageLong = 842;
    private const double MaxPageShort = 595;
    private const double ScreenDpi = 96;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { "bmp", "png", "gif", "jpg", "jpeg", "jpe", "tif", "tiff" };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase) { "txt", "log" };

    // PDFs are opened, not converted; Nero PDF itself may be the program behind their printto command.
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase) { "pdf" };

    private readonly string _portPrefix = global::Windows.ApplicationModel.Package.Current.Id.FamilyName + "_";
    private ConvertibleTypes? _types;

    public bool IsSupported => true;

    public ConvertibleTypes GetConvertibleTypes() => _types ??= FindConvertibleTypes();

    public ConvertOutcome Convert(string path, string folder)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        if (ImageExtensions.Contains(extension))
        {
            try
            {
                return new ConvertOutcome(ConvertResult.Pages, WiaImages.OnStaThread(() => WiaImages.SaveFramesAsJpeg(path, folder, PageDpi)));
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                return new ConvertOutcome(ConvertResult.Failed);
            }
        }

        var printer = FindPrinterName();
        if (printer is null)
        {
            return new ConvertOutcome(ConvertResult.NoPrinter);
        }
        try
        {
            if (TextExtensions.Contains(extension))
            {
                Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { "/pt", path, printer } });
                return new ConvertOutcome(ConvertResult.Printing);
            }
            if (!HasPrintToCommand("." + extension))
            {
                return new ConvertOutcome(ConvertResult.NotConvertible);
            }
            Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = PrintToVerb, Arguments = "\"" + printer + "\"" });
            return new ConvertOutcome(ConvertResult.Printing);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return new ConvertOutcome(ConvertResult.Failed);
        }
    }

    private static void Start(ProcessStartInfo info)
    {
        info.WindowStyle = ProcessWindowStyle.Minimized;
        using var process = Process.Start(info);
    }

    /// <summary>Screen resolution, or higher where needed so that the page fits on A4.</summary>
    private static int PageDpi(int width, int height)
    {
        var (maxWidth, maxHeight) = width > height ? (MaxPageLong, MaxPageShort) : (MaxPageShort, MaxPageLong);
        var dpi = Math.Max(ScreenDpi, Math.Max(width * 72 / maxWidth, height * 72 / maxHeight));
        return (int)Math.Ceiling(dpi);
    }

    private static ConvertibleTypes FindConvertibleTypes()
    {
        var documents = new HashSet<string>(TextExtensions, StringComparer.Ordinal);
        foreach (var name in Registry.ClassesRoot.GetSubKeyNames())
        {
            if (name.Length > 1 && name[0] == '.' && !Excluded.Contains(name[1..]) && !ImageExtensions.Contains(name[1..])
                && HasPrintToCommand(name))
            {
                documents.Add(name[1..].ToLowerInvariant());
            }
        }
        return new ConvertibleTypes(ImageExtensions.Order(StringComparer.Ordinal).ToList(), documents.Order(StringComparer.Ordinal).ToList());
    }

    private static bool HasPrintToCommand(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }
        uint length = 1024;
        var buffer = new StringBuilder((int)length);
        return AssocQueryString(0, AssocStringCommand, extension, PrintToVerb, buffer, ref length) == 0 && buffer.Length > 0;
    }

    /// <summary>The printer name is localized; the printer is found by its port, which starts with the package family name.</summary>
    private string? FindPrinterName()
    {
        const int level = 5;
        EnumPrinters(PrinterEnumLocal, null, level, IntPtr.Zero, 0, out var needed, out _);
        if (needed == 0)
        {
            return null;
        }
        var buffer = Marshal.AllocHGlobal(needed);
        try
        {
            if (!EnumPrinters(PrinterEnumLocal, null, level, buffer, needed, out _, out var count))
            {
                return null;
            }
            var size = Marshal.SizeOf<PrinterInfo5>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<PrinterInfo5>(buffer + i * size);
                if (info.PortName?.StartsWith(_portPrefix, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return info.PrinterName;
                }
            }
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private const int PrinterEnumLocal = 0x2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrinterInfo5
    {
        public string? PrinterName;
        public string? PortName;
        public uint Attributes;
        public uint DeviceNotSelectedTimeout;
        public uint TransmissionRetryTimeout;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumPrinters(int flags, string? name, int level, IntPtr buffer, int size, out int needed, out int returned);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags, uint str, string assoc, string? extra, StringBuilder? output, ref uint length);
}
