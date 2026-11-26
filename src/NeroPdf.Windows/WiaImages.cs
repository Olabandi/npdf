using NeroPdf.Core.Platform;

namespace NeroPdf.Windows;

/// <summary>
/// Image handling through the WIA automation library that is part of Windows: loading image files and
/// converting images to JPEG. WIA objects are apartment-threaded, so callers use <see cref="OnStaThread"/>.
/// </summary>
internal static class WiaImages
{
    public const string JpegFormat = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
    private const int JpegQuality = 85;

    /// <summary>Returns the image as JPEG; many scanner drivers and most image files deliver another format.</summary>
    public static dynamic AsJpeg(dynamic image)
    {
        if (string.Equals((string)image.FormatID, JpegFormat, StringComparison.OrdinalIgnoreCase))
        {
            return image;
        }
        dynamic process = Create("WIA.ImageProcess");
        process.Filters.Add(process.FilterInfos["Convert"].FilterID);
        process.Filters[1].Properties["FormatID"].Value = JpegFormat;
        process.Filters[1].Properties["Quality"].Value = JpegQuality;
        return process.Apply(image);
    }

    /// <summary>Loads an image file (BMP, PNG, GIF, JPEG, TIFF); every frame of a TIFF or GIF becomes a page.</summary>
    public static IReadOnlyList<ScannedImage> SaveFramesAsJpeg(string file, string folder, Func<int, int, int> pageDpi)
    {
        Directory.CreateDirectory(folder);
        dynamic image = Create("WIA.ImageFile");
        image.LoadFile(file);
        var frames = Math.Max(1, (int)image.FrameCount);
        var pages = new List<ScannedImage>();
        for (var frame = 1; frame <= frames; frame++)
        {
            image.ActiveFrame = frame;
            dynamic jpeg = AsJpeg(image);
            var path = Path.Combine(folder, $"page-{frame:D4}.jpg");
            File.Delete(path);
            jpeg.SaveFile(path);
            pages.Add(new ScannedImage(path, pageDpi((int)jpeg.Width, (int)jpeg.Height)));
        }
        return pages;
    }

    public static dynamic Create(string progId)
    {
        var type = Type.GetTypeFromProgID(progId) ?? throw new InvalidOperationException($"{progId} is not available.");
        return Activator.CreateInstance(type)!;
    }

    public static T OnStaThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        { IsBackground = true, Name = "WIA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }
        return result;
    }
}
