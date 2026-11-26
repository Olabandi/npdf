using Microsoft.Extensions.Logging.Abstractions;
using NeroPdf.Core.Documents;
using NeroPdf.Core.Pdf;
using NeroPdf.Core.Pdf.Pdfium;
using NeroPdf.Core.Platform;

namespace NeroPdf.Tests;

/// <summary>PDFs made from scanned images, and where Nero PDF puts documents it creates itself.</summary>
public sealed class ScanDocumentTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "NeroPdfTests", Guid.NewGuid().ToString("N"));
    private readonly PdfiumEngine _engine = new(NullLogger<PdfiumEngine>.Instance);

    private static string ScanImage => Path.Combine(RepoPaths.Root, "tests", "NeroPdf.Tests", "TestImages", "scan-page.jpg");

    public ScanDocumentTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void ImagesBecomePagesOfTheirSize()
    {
        // The test image is 300 × 450 pixels: at 300 dpi that is 1 × 1.5 inch, at 150 dpi twice as large.
        var path = Path.Combine(_folder, "scan.pdf");
        using (var output = File.Create(path))
        {
            _engine.CreateFromImages([new PdfImagePage(ScanImage, 300), new PdfImagePage(ScanImage, 150)], output);
        }

        var result = _engine.Open(path);
        Assert.Equal(PdfOpenStatus.Ok, result.Status);
        using var document = result.Document!;
        Assert.Equal(2, document.PageCount);
        AssertSize(72, 108, document.GetPageSize(0));
        AssertSize(144, 216, document.GetPageSize(1));

        // The image is really on the page: the blue bar near the top is not white.
        var bitmap = document.RenderPage(0, 100, 150);
        var offset = 20 * bitmap.Stride + 50 * 4;
        Assert.True(bitmap.Pixels[offset] > bitmap.Pixels[offset + 2] + 40, "Expected the blue bar of the scanned image.");
    }

    [Theory]
    [InlineData(false, 1.0)]
    [InlineData(true, 1.0)]
    [InlineData(false, 0.1)]
    public void ImagesOnA4AreCentredAndScaled(bool landscape, double zoom)
    {
        var path = Path.Combine(_folder, "a4.pdf");
        using (var output = File.Create(path))
        {
            _engine.CreateFromImages([new PdfImagePage(ScanImage, 300, PdfPageLayout.A4(landscape, zoom))], output);
        }
        using var document = _engine.Open(path).Document!;
        AssertSize(landscape ? 841.89 : 595.28, landscape ? 595.28 : 841.89, document.GetPageSize(0));

        // The test image (2:3) is as high as the page at 100 %, a tenth of that at 10 %, always centred:
        // the corners of the page stay white, and the middle of the blue bar sits on the middle line.
        var bitmap = document.RenderPage(0, landscape ? 300 : 200, landscape ? 200 : 300);
        Assert.Equal(255, bitmap.Pixels[0]);
        var imageHeight = bitmap.Height * zoom;
        var imageTop = (bitmap.Height - imageHeight) / 2;
        var barRow = (int)(imageTop + imageHeight * 80 / 450);
        var middle = bitmap.Width / 2;
        var offset = barRow * bitmap.Stride + middle * 4;
        Assert.True(bitmap.Pixels[offset] > bitmap.Pixels[offset + 2] + 40, "Expected the blue bar in the middle of the page.");
    }

    [Fact]
    public void ScannedDocumentStaysLoadableAfterPageOperations()
    {
        var path = Path.Combine(_folder, "scan.pdf");
        using (var output = File.Create(path))
        {
            _engine.CreateFromImages([new PdfImagePage(ScanImage, 300), new PdfImagePage(ScanImage, 300), new PdfImagePage(ScanImage, 300)], output);
        }
        using (var document = _engine.Open(path).Document!)
        {
            document.RotatePages([0], 1);
            document.DeletePages([1]);
            using var edited = File.Create(Path.Combine(_folder, "edited.pdf"));
            document.Save(edited);
        }
        using var reopened = _engine.Open(Path.Combine(_folder, "edited.pdf")).Document!;
        Assert.Equal(2, reopened.PageCount);
    }

    [Fact]
    public void NoImagesIsRejected()
    {
        using var output = new MemoryStream();
        Assert.Throws<ArgumentException>(() => _engine.CreateFromImages([], output));
    }

    [Fact]
    public void BrokenImageIsReportedAndNothingIsLeftOpen()
    {
        var broken = Path.Combine(_folder, "broken.jpg");
        File.WriteAllBytes(broken, [0x01, 0x02, 0x03]);
        using var output = new MemoryStream();
        Assert.Throws<InvalidDataException>(() => _engine.CreateFromImages([new PdfImagePage(broken, 300)], output));
    }

    [Fact]
    public void CreatedDocumentsCountAsPrintedAndCarryTheTitle()
    {
        var inbox = new FolderPrintInbox(_folder);
        var path = inbox.CreateDocumentPath("Scan");
        Assert.Equal(Path.Combine(_folder, PrintFolders.Opened), Path.GetDirectoryName(path));
        Assert.Equal("Scan", PrintFileName.TitleOf(path));
        Assert.NotEqual(path, inbox.CreateDocumentPath("Scan"));
        // Never claimed by another window: only completed print jobs in Incoming are.
        File.WriteAllText(path, "x");
        Assert.Null(inbox.TryClaimNext());
    }

    [Fact]
    public void ConvertedPrintoutKnowsItsOriginOnce()
    {
        var inbox = new FolderPrintInbox(_folder);
        var origin = Path.Combine(_folder, "Letter.docx");
        inbox.RegisterConversion(origin);
        var printed = Path.Combine(_folder, PrintFileName.Create("Microsoft Word - Letter.docx", DateTime.Now, Guid.NewGuid()) + ".pdf");
        var other = Path.Combine(_folder, PrintFileName.Create("Invoice", DateTime.Now, Guid.NewGuid()) + ".pdf");

        Assert.Null(inbox.TakeConversionOrigin(other));
        Assert.Equal(origin, inbox.TakeConversionOrigin(printed));
        Assert.Null(inbox.TakeConversionOrigin(printed));
    }

    private static void AssertSize(double width, double height, PdfPageSize size)
    {
        Assert.Equal(width, size.Width, 1);
        Assert.Equal(height, size.Height, 1);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
