namespace NeroPdf.Core.Platform;

/// <summary>
/// Scanning and converting are unlocked when a Nero suite (Nero Start / Nero Platinum) is installed.
/// Deliberately simple check for now.
/// </summary>
public interface INeroSuite
{
    bool IsInstalled();
}

/// <summary>
/// Converts other files to PDF: images directly (one page per image), everything else by printing it
/// on the Nero PDF printer.
/// </summary>
public interface IFileConverter
{
    /// <summary>False where the platform cannot do it (no virtual printer, e.g. development build or macOS).</summary>
    bool IsSupported { get; }

    /// <summary>Extensions (without dot, lower case) of the file types that can be converted.</summary>
    IReadOnlyList<string> GetConvertibleExtensions();

    /// <summary>
    /// Images: written as JPEG pages into <paramref name="folder"/>; the app makes the PDF.
    /// Other files: handed to the program registered for their type, which prints them on the Nero PDF
    /// printer; the result arrives like any other print job (this does not wait for it).
    /// </summary>
    ConvertOutcome Convert(string path, string folder);
}

public enum ConvertResult
{
    /// <summary>Sent to the printer; the result opens in its own window.</summary>
    Printing,

    /// <summary>The pages are ready as images.</summary>
    Pages,
    NoPrinter,
    NotConvertible,
    Failed,
}

public sealed record ConvertOutcome(ConvertResult Result, IReadOnlyList<ScannedImage>? Pages = null);

/// <summary>Scanners of the system. Calls may block; the app runs them in the background.</summary>
public interface IScanner
{
    IReadOnlyList<ScannerDevice> GetDevices();

    /// <summary>
    /// Scans from the document feeder all sheets, from the flatbed one page, and writes one JPEG file
    /// per page into <paramref name="folder"/>.
    /// </summary>
    /// <exception cref="ScanException">The scanner reported an error.</exception>
    IReadOnlyList<ScannedImage> Scan(ScannerDevice device, ScanSettings settings, string folder);
}

public sealed record ScannerDevice(string Id, string Name, bool HasFeeder, bool HasFlatbed);

public enum ScanSource
{
    Flatbed,
    Feeder,
}

public enum ScanColor
{
    Color,
    Gray,
}

public sealed record ScanSettings(ScanSource Source, ScanColor Color, int Dpi);

/// <summary>A page as JPEG image (from the scanner or a converted image file) and its resolution.</summary>
public sealed record ScannedImage(string JpegPath, int Dpi);

public enum ScanError
{
    Failed,
    NoPaper,
    Offline,
}

public sealed class ScanException(ScanError error, string message, Exception? inner = null) : Exception(message, inner)
{
    public ScanError Error { get; } = error;
}

/// <summary>Used where scanning and converting are not available (e.g. macOS for now).</summary>
public sealed class UnsupportedExtras : INeroSuite, IFileConverter, IScanner
{
    public bool IsSupported => false;

    public bool IsInstalled() => false;

    public IReadOnlyList<string> GetConvertibleExtensions() => [];

    public ConvertOutcome Convert(string path, string folder) => new(ConvertResult.NoPrinter);

    public IReadOnlyList<ScannerDevice> GetDevices() => [];

    public IReadOnlyList<ScannedImage> Scan(ScannerDevice device, ScanSettings settings, string folder) =>
        throw new ScanException(ScanError.Offline, "Scanning is not available on this platform.");
}
