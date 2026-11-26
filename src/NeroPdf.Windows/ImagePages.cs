using NeroPdf.Core.Platform;
using SkiaSharp;

namespace NeroPdf.Windows;

/// <summary>
/// Turns image files into JPEG pages for the PDF. JPEG has no transparency, so transparent areas are
/// put on white, like the white page they end up on (otherwise they would turn black).
/// Decoding uses SkiaSharp, which ships with Avalonia anyway; TIFF, which Skia cannot read, goes through WIA.
/// </summary>
internal static class ImagePages
{
    private const int JpegQuality = 85;

    private static readonly HashSet<string> WiaOnly = new(StringComparer.OrdinalIgnoreCase) { ".tif", ".tiff" };

    /// <summary>One page per image, or per frame of an animated GIF.</summary>
    public static IReadOnlyList<ScannedImage> SaveAsJpeg(string file, string folder, Func<int, int, int> pageDpi)
    {
        Directory.CreateDirectory(folder);
        if (WiaOnly.Contains(Path.GetExtension(file)))
        {
            return WiaImages.OnStaThread(() => WiaImages.SaveFramesAsJpeg(file, folder, pageDpi));
        }

        using var codec = SKCodec.Create(file) ?? throw new InvalidDataException("The image could not be read.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var frames = Math.Max(1, codec.FrameCount);
        var pages = new List<ScannedImage>(frames);
        for (var frame = 0; frame < frames; frame++)
        {
            using var bitmap = new SKBitmap(info);
            var result = codec.GetPixels(info, bitmap.GetPixels(), new SKCodecOptions(frame));
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                throw new InvalidDataException($"The image could not be decoded: {result}.");
            }
            var path = Path.Combine(folder, $"page-{frame + 1:D4}.jpg");
            WriteOnWhite(bitmap, path);
            pages.Add(new ScannedImage(path, pageDpi(info.Width, info.Height)));
        }
        return pages;
    }

    private static void WriteOnWhite(SKBitmap bitmap, string path)
    {
        using var flat = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(flat))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(bitmap, 0, 0);
        }
        using var output = File.Create(path);
        if (!flat.Encode(output, SKEncodedImageFormat.Jpeg, JpegQuality))
        {
            throw new InvalidOperationException("The image could not be encoded as JPEG.");
        }
    }
}
