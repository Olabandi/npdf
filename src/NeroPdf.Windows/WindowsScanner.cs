using System.Runtime.InteropServices;
using NeroPdf.Core.Platform;
using Windows.Devices.Enumeration;
using Windows.Devices.Scanners;
using Windows.Storage;

namespace NeroPdf.Windows;

/// <summary>
/// Scanners through the scanner API of Windows (Windows.Devices.Scanners, built on WIA 2). It scans
/// all sheets of the document feeder in one go, which the older WIA automation layer could not do
/// reliably (e.g. Brother drivers report "busy" after the first sheet).
/// Images that the scanner does not deliver as JPEG are converted with WIA.
/// macOS counterpart: ImageCaptureCore, with the Mac build (until then scanning is locked there).
/// </summary>
internal sealed class WindowsScanner : IScanner
{
    // A busy scanner is asked again every second, ten times; then the user decides.
    private const int BusyRetries = 10;
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromSeconds(1);

    // WIA error codes, also reported by the Windows scanner API.
    private const int PaperEmpty = unchecked((int)0x80210003);
    private const int PaperJam = unchecked((int)0x80210002);
    private const int Offline = unchecked((int)0x80210005);
    private const int Busy = unchecked((int)0x80210006);
    private const int CoverOpen = unchecked((int)0x80210016);
    private const int NoDevice = unchecked((int)0x80210015);
    private const int Communication = unchecked((int)0x8021000A);

    public IReadOnlyList<ScannerDevice> GetDevices()
    {
        var result = new List<ScannerDevice>();
        var devices = DeviceInformation.FindAllAsync(ImageScanner.GetDeviceSelector()).AsTask().GetAwaiter().GetResult();
        foreach (var device in devices)
        {
            try
            {
                var scanner = ImageScanner.FromIdAsync(device.Id).AsTask().GetAwaiter().GetResult();
                if (scanner is null)
                {
                    continue;
                }
                var feeder = scanner.IsScanSourceSupported(ImageScannerScanSource.Feeder);
                var flatbed = scanner.IsScanSourceSupported(ImageScannerScanSource.Flatbed) || !feeder;
                result.Add(new ScannerDevice(device.Id, device.Name, feeder, flatbed));
            }
            catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
            {
                // Switched off or busy: listed anyway; scanning will report the error.
                result.Add(new ScannerDevice(device.Id, device.Name, HasFeeder: false, HasFlatbed: true));
            }
        }
        return result;
    }

    public IReadOnlyList<ScannedImage> Scan(ScannerDevice device, ScanSettings settings, string folder, IProgress<int>? progress = null)
    {
        Directory.CreateDirectory(folder);
        try
        {
            var scanner = ImageScanner.FromIdAsync(device.Id).AsTask().GetAwaiter().GetResult()
                ?? throw new ScanException(ScanError.Offline, "The scanner is no longer connected.");
            var source = settings.Source == ScanSource.Feeder ? ImageScannerScanSource.Feeder : ImageScannerScanSource.Flatbed;
            var dpi = Configure(scanner, source, settings);
            var target = StorageFolder.GetFolderFromPathAsync(folder).AsTask().GetAwaiter().GetResult();
            var result = ScanWithRetry(scanner, source, target, progress);
            var pages = new List<ScannedImage>();
            foreach (var file in result.ScannedFiles)
            {
                pages.AddRange(AsJpegPages(file.Path, folder, pages.Count, dpi));
            }
            if (pages.Count == 0)
            {
                throw new ScanException(source == ImageScannerScanSource.Feeder ? ScanError.NoPaper : ScanError.Failed, "Nothing was scanned.");
            }
            return pages;
        }
        catch (COMException ex)
        {
            var error = ex.HResult switch
            {
                PaperEmpty or PaperJam => ScanError.NoPaper,
                Busy => ScanError.Busy,
                Offline or CoverOpen or NoDevice or Communication => ScanError.Offline,
                _ => ScanError.Failed,
            };
            throw new ScanException(error, $"Scanner error 0x{ex.HResult:X8}", ex);
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            throw new ScanException(ScanError.Failed, "Scanning failed: " + ex.GetType().Name, ex);
        }
    }

    /// <summary>Colour, resolution and JPEG where the scanner supports them; returns the resolution used.</summary>
    private static int Configure(ImageScanner scanner, ImageScannerScanSource source, ScanSettings settings)
    {
        IImageScannerFormatConfiguration formats = source == ImageScannerScanSource.Feeder ? scanner.FeederConfiguration : scanner.FlatbedConfiguration;
        if (formats.IsFormatSupported(ImageScannerFormat.Jpeg))
        {
            formats.Format = ImageScannerFormat.Jpeg;
        }

        if (source == ImageScannerScanSource.Feeder)
        {
            var feeder = scanner.FeederConfiguration;
            feeder.MaxNumberOfPages = 0; // all sheets
            feeder.Duplex = false;
            return ApplyColorAndResolution(feeder, settings);
        }
        return ApplyColorAndResolution(scanner.FlatbedConfiguration, settings);
    }

    private static int ApplyColorAndResolution(IImageScannerSourceConfiguration configuration, ScanSettings settings)
    {
        var mode = settings.Color == ScanColor.Color ? ImageScannerColorMode.Color : ImageScannerColorMode.Grayscale;
        if (configuration.IsColorModeSupported(mode))
        {
            configuration.ColorMode = mode;
        }
        var min = configuration.MinResolution;
        var max = configuration.MaxResolution;
        var dpi = Math.Clamp(settings.Dpi, (int)Math.Ceiling(min.DpiX), Math.Max((int)Math.Ceiling(min.DpiX), (int)max.DpiX));
        configuration.DesiredResolution = new ImageScannerResolution { DpiX = dpi, DpiY = dpi };
        var actual = configuration.ActualResolution.DpiX;
        return actual > 0 ? (int)Math.Round(actual) : dpi;
    }

    private static ImageScannerScanResult ScanWithRetry(ImageScanner scanner, ImageScannerScanSource source, StorageFolder target,
        IProgress<int>? progress)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // The scanner reports each finished page; the app shows "… so far N pages".
                var operation = scanner.ScanFilesToFolderAsync(source, target);
                operation.Progress = (_, pages) => progress?.Report((int)pages);
                return operation.AsTask().GetAwaiter().GetResult();
            }
            catch (COMException ex) when (ex.HResult == Busy && attempt < BusyRetries)
            {
                Thread.Sleep(BusyRetryDelay);
            }
        }
    }

    /// <summary>JPEG files are used as they are; other formats (bitmap, PNG, TIFF) are converted page by page.</summary>
    private static IEnumerable<ScannedImage> AsJpegPages(string file, string folder, int pagesSoFar, int dpi)
    {
        var extension = Path.GetExtension(file);
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return [new ScannedImage(file, dpi)];
        }
        var pageFolder = Path.Combine(folder, $"converted-{pagesSoFar + 1:D4}");
        return WiaImages.OnStaThread(() => WiaImages.SaveFramesAsJpeg(file, pageFolder, (_, _) => dpi));
    }
}
