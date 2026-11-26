using System.Runtime.InteropServices;
using NeroPdf.Core.Platform;

namespace NeroPdf.Windows;

/// <summary>
/// Scanners via WIA (Windows Image Acquisition), through its automation library that is part of
/// Windows. WIA objects are apartment-threaded, so every call runs on its own STA thread.
/// macOS counterpart: ImageCaptureCore, with the Mac build (until then scanning is locked there).
/// </summary>
internal sealed class WiaScanner : IScanner
{
    private const int ScannerDeviceType = 1;

    // WIA property ids.
    private const int DocumentHandlingCapabilities = 3086;
    private const int DocumentHandlingSelect = 3088;
    private const int Pages = 3096;
    private const int CurrentIntent = 6146;
    private const int HorizontalResolution = 6147;
    private const int VerticalResolution = 6148;

    private const int Feeder = 0x1;
    private const int Flatbed = 0x2;
    private const int IntentColor = 1;
    private const int IntentGray = 2;

    // WIA error codes.
    private const int PaperEmpty = unchecked((int)0x80210003);
    private const int PaperJam = unchecked((int)0x80210002);
    private const int Offline = unchecked((int)0x80210005);
    private const int Busy = unchecked((int)0x80210006);
    private const int CoverOpen = unchecked((int)0x80210016);
    private const int NoDevice = unchecked((int)0x80210015);
    private const int Communication = unchecked((int)0x8021000A);

    public IReadOnlyList<ScannerDevice> GetDevices() => WiaImages.OnStaThread(() =>
    {
        var result = new List<ScannerDevice>();
        dynamic? manager = CreateDeviceManager();
        if (manager is null)
        {
            return result;
        }
        foreach (dynamic info in manager.DeviceInfos)
        {
            if ((int)info.Type != ScannerDeviceType)
            {
                continue;
            }
            string id = info.DeviceID;
            string name = PropertyValue(info.Properties, "Name") as string ?? id;
            var capabilities = Flatbed;
            try
            {
                dynamic device = info.Connect();
                capabilities = PropertyValue(device.Properties, DocumentHandlingCapabilities) is int value ? value : Flatbed;
            }
            catch (COMException)
            {
                // Switched off or busy: still listed; scanning will report the error.
            }
            result.Add(new ScannerDevice(id, name, (capabilities & Feeder) != 0, (capabilities & Flatbed) != 0 || (capabilities & Feeder) == 0));
        }
        return result;
    });

    public IReadOnlyList<ScannedImage> Scan(ScannerDevice scanner, ScanSettings settings, string folder) => WiaImages.OnStaThread(() =>
    {
        Directory.CreateDirectory(folder);
        dynamic manager = CreateDeviceManager() ?? throw new ScanException(ScanError.Offline, "WIA is not available.");
        dynamic? info = null;
        foreach (dynamic candidate in manager.DeviceInfos)
        {
            if ((string)candidate.DeviceID == scanner.Id)
            {
                info = candidate;
            }
        }
        if (info is null)
        {
            throw new ScanException(ScanError.Offline, "The scanner is no longer connected.");
        }

        var images = new List<ScannedImage>();
        try
        {
            dynamic device = info.Connect();
            var fromFeeder = settings.Source == ScanSource.Feeder;
            TrySetProperty(device.Properties, DocumentHandlingSelect, fromFeeder ? Feeder : Flatbed);
            TrySetProperty(device.Properties, Pages, fromFeeder ? 0 : 1);
            dynamic item = device.Items[1];
            TrySetProperty(item.Properties, CurrentIntent, settings.Color == ScanColor.Color ? IntentColor : IntentGray);
            TrySetProperty(item.Properties, HorizontalResolution, settings.Dpi);
            TrySetProperty(item.Properties, VerticalResolution, settings.Dpi);

            while (true)
            {
                dynamic image;
                try
                {
                    image = item.Transfer(WiaImages.JpegFormat);
                }
                catch (COMException ex) when (ex.HResult == PaperEmpty && fromFeeder && images.Count > 0)
                {
                    break; // All sheets of the feeder are scanned.
                }
                image = WiaImages.AsJpeg(image);
                var path = Path.Combine(folder, $"page-{images.Count + 1:D4}.jpg");
                File.Delete(path);
                image.SaveFile(path);
                var dpi = image.HorizontalResolution is double resolution && resolution > 0 ? (int)Math.Round(resolution) : settings.Dpi;
                images.Add(new ScannedImage(path, dpi));
                if (!fromFeeder)
                {
                    break;
                }
            }
            return images;
        }
        catch (COMException ex)
        {
            var error = ex.HResult switch
            {
                PaperEmpty or PaperJam => ScanError.NoPaper,
                Offline or Busy or CoverOpen or NoDevice or Communication => ScanError.Offline,
                _ => ScanError.Failed,
            };
            throw new ScanException(error, $"WIA error 0x{ex.HResult:X8}", ex);
        }
    });

    private static dynamic? CreateDeviceManager()
    {
        var type = Type.GetTypeFromProgID("WIA.DeviceManager");
        return type is null ? null : Activator.CreateInstance(type);
    }

    private static object? PropertyValue(dynamic properties, object id)
    {
        foreach (dynamic property in properties)
        {
            if (id is int number ? (int)property.PropertyID == number : (string)property.Name == (string)id)
            {
                return property.Value;
            }
        }
        return null;
    }

    /// <summary>Not every scanner supports every value; then the scanner's own setting stays.</summary>
    private static void TrySetProperty(dynamic properties, int id, int value)
    {
        foreach (dynamic property in properties)
        {
            if ((int)property.PropertyID == id)
            {
                try
                {
                    property.Value = value;
                }
                catch (COMException)
                {
                }
                return;
            }
        }
    }
}
