using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NeroPdf.Core.Pdf;
using NeroPdf.Core.Pdf.Pdfium;

namespace NeroPdf.Tests;

/// <summary>
/// Runs over the whole TestFiles collection and checks the expected behaviour from the concept table.
/// </summary>
public class TestFileCollectionTests
{
    // Only used here, never in program code or configuration.
    private const string TestFilePassword = "test123";

    private static readonly PdfiumEngine Engine = new(NullLogger<PdfiumEngine>.Instance);

    /// <summary>File, expected status, expected page count (0 = any), page is blank.</summary>
    public static TheoryData<string, PdfOpenStatus, int, bool> Expectations => new()
    {
        { "sample-blank-a4.pdf", PdfOpenStatus.Ok, 1, true },
        { "sample-blank-letter.pdf", PdfOpenStatus.Ok, 1, true },
        { "blank-pages.pdf", PdfOpenStatus.Ok, 5, false },
        { "sample-1-page.pdf", PdfOpenStatus.Ok, 1, false },
        { "sample-10-pages.pdf", PdfOpenStatus.Ok, 10, false },
        { "multi-page-report.pdf", PdfOpenStatus.Ok, 12, false },
        { "sample-100-pages.pdf", PdfOpenStatus.Ok, 100, false },
        { "sample-500-pages.pdf", PdfOpenStatus.Ok, 500, false },
        { "mixed-rotation.pdf", PdfOpenStatus.Ok, 4, false },
        { "sample-pdf-100kb.pdf", PdfOpenStatus.Ok, 0, false },
        { "sample-pdf-500kb.pdf", PdfOpenStatus.Ok, 0, false },
        { "sample-pdf-1mb.pdf", PdfOpenStatus.Ok, 0, false },
        { "sample-pdf-5mb.pdf", PdfOpenStatus.Ok, 0, false },
        { "sample-pdf-10mb.pdf", PdfOpenStatus.Ok, 0, false },
        { "sample-form.pdf", PdfOpenStatus.Ok, 1, false },
        { "sample-form-filled.pdf", PdfOpenStatus.Ok, 1, false },
        { "sample-password-protected.pdf", PdfOpenStatus.PasswordRequired, 0, false },
        { "sample-permissions-restricted.pdf", PdfOpenStatus.Ok, 3, false },
        { "sample-scanned.pdf", PdfOpenStatus.Ok, 3, false },
        { "sample-with-attachments.pdf", PdfOpenStatus.Ok, 2, false },
        { "sample-invoice.pdf", PdfOpenStatus.Ok, 1, false },
        { "sample-corrupted.pdf", PdfOpenStatus.Damaged, 0, false },
    };

    [Fact]
    public void EveryTestFileHasAnExpectation()
    {
        var expected = Expectations.Select(row => row.Data.Item1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var present = Directory.GetFiles(RepoPaths.TestFiles, "*.pdf").Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expected.Order(), present.Order()!);
    }

    [Theory]
    [MemberData(nameof(Expectations))]
    public void OpensAsExpectedAndRendersFirstPage(string file, PdfOpenStatus status, int pages, bool blank)
    {
        var result = Engine.Open(RepoPaths.TestFile(file));
        using var document = result.Document;

        Assert.Equal(status, result.Status);
        if (status != PdfOpenStatus.Ok)
        {
            Assert.Null(document);
            return;
        }

        Assert.NotNull(document);
        Assert.True(document.PageCount > 0);
        if (pages > 0)
        {
            Assert.Equal(pages, document.PageCount);
        }

        var bitmap = RenderFitWidth(document, 0, 200);
        Assert.Equal(blank, IsAllWhite(bitmap));
    }

    [Fact]
    public void PasswordProtectedFileOpensWithCorrectPassword()
    {
        var result = Engine.Open(RepoPaths.TestFile("sample-password-protected.pdf"), TestFilePassword);
        using var document = result.Document;

        Assert.Equal(PdfOpenStatus.Ok, result.Status);
        Assert.Equal(3, document!.PageCount);
        Assert.False(IsAllWhite(RenderFitWidth(document, 0, 200)));
    }

    [Fact]
    public void PasswordProtectedFileReportsWrongPassword()
    {
        var result = Engine.Open(RepoPaths.TestFile("sample-password-protected.pdf"), "wrong");

        Assert.Equal(PdfOpenStatus.WrongPassword, result.Status);
        Assert.Null(result.Document);
    }

    [Fact]
    public void MissingFileReportsNotFound()
    {
        var result = Engine.Open(Path.Combine(RepoPaths.TestFiles, "does-not-exist.pdf"));

        Assert.Equal(PdfOpenStatus.NotFound, result.Status);
    }

    [Fact]
    public void PageFormatsAreRecognized()
    {
        using var a4 = Engine.Open(RepoPaths.TestFile("sample-blank-a4.pdf")).Document!;
        using var letter = Engine.Open(RepoPaths.TestFile("sample-blank-letter.pdf")).Document!;

        Assert.Equal(new PdfPageSize(595, 842), Round(a4.GetPageSize(0)));
        Assert.Equal(new PdfPageSize(612, 792), Round(letter.GetPageSize(0)));
    }

    [Fact]
    public void MixedRotationPagesAreShownInTheirOrientation()
    {
        using var document = Engine.Open(RepoPaths.TestFile("mixed-rotation.pdf")).Document!;
        var sizes = Enumerable.Range(0, 4).Select(document.GetPageSize).ToArray();

        // Pages rotated by 90 and 270 degrees are displayed in landscape.
        Assert.True(sizes[0].Height > sizes[0].Width);
        Assert.True(sizes[1].Width > sizes[1].Height);
        Assert.True(sizes[2].Height > sizes[2].Width);
        Assert.True(sizes[3].Width > sizes[3].Height);
    }

    [Fact]
    public void FileIsNotLockedWhileDocumentIsOpen()
    {
        var copy = Path.Combine(Path.GetTempPath(), "NeroPdfTest_" + Guid.NewGuid().ToString("N") + ".pdf");
        File.Copy(RepoPaths.TestFile("sample-10-pages.pdf"), copy);
        using var document = Engine.Open(copy).Document!;

        File.Delete(copy);

        Assert.False(File.Exists(copy));
        Assert.Equal(10, document.PageCount);
    }

    [Fact]
    public void FirstPageOfSmallDocumentIsQuick()
    {
        var watch = Stopwatch.StartNew();
        using (var document = Engine.Open(RepoPaths.TestFile("sample-1-page.pdf")).Document!)
        {
            RenderFitWidth(document, 0, 1200);
        }
        watch.Stop();

        TestContext.Current.SendDiagnosticMessage($"Open and render first page: {watch.ElapsedMilliseconds} ms");
        Assert.True(watch.ElapsedMilliseconds < 1000, $"Took {watch.ElapsedMilliseconds} ms");
    }

    private static PdfBitmap RenderFitWidth(IPdfDocument document, int index, int width)
    {
        var size = document.GetPageSize(index);
        var height = Math.Max(1, (int)Math.Round(width * size.Height / size.Width));
        return document.RenderPage(index, width, height);
    }

    private static bool IsAllWhite(PdfBitmap bitmap) => bitmap.Pixels.All(b => b == 0xFF);

    private static PdfPageSize Round(PdfPageSize size) => new(Math.Round(size.Width), Math.Round(size.Height));
}
