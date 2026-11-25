using Microsoft.Extensions.Logging.Abstractions;
using NeroPdf.Core.Documents;
using NeroPdf.Core.Pdf;
using NeroPdf.Core.Pdf.Pdfium;

namespace NeroPdf.Tests;

/// <summary>Rotate, delete, reorder and save; the result must stay loadable (concept: tests from step 4 on).</summary>
public sealed class PageOperationTests : IDisposable
{
    private const string TestFilePassword = "test123";

    private readonly string _dir = Directory.CreateTempSubdirectory("NeroPdfOps_").FullName;
    private readonly PdfiumEngine _engine = new(NullLogger<PdfiumEngine>.Instance);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    public static TheoryData<string> OpenableFiles => new(
        TestFileCollectionTests.Expectations
            .Where(row => row.Data.Item2 == PdfOpenStatus.Ok)
            .Select(row => row.Data.Item1));

    [Theory]
    [MemberData(nameof(OpenableFiles))]
    public void EditedFileCanBeSavedAndReopened(string file)
    {
        using var document = _engine.Open(RepoPaths.TestFile(file)).Document!;
        var pages = document.PageCount;
        var firstSize = document.GetPageSize(0);

        document.RotatePages([0], 1);
        if (pages > 1)
        {
            document.MovePages([0], pages - 1);
            document.DeletePages([0]);
        }
        Assert.True(document.IsModified);

        var saved = Save(document, file);
        using var reopened = _engine.Open(saved).Document;

        Assert.NotNull(reopened);
        Assert.Equal(pages > 1 ? pages - 1 : 1, reopened.PageCount);
        var rotated = reopened.GetPageSize(reopened.PageCount - 1);
        Assert.Equal(Math.Round(firstSize.Width), Math.Round(rotated.Height));
        Assert.Equal(Math.Round(firstSize.Height), Math.Round(rotated.Width));
        reopened.RenderPage(0, 100, 140);
    }

    [Fact]
    public void RotationContinuesFromTheExistingRotation()
    {
        using var document = _engine.Open(RepoPaths.TestFile("mixed-rotation.pdf")).Document!;
        var before = Enumerable.Range(0, 4).Select(document.GetPageSize).ToArray();

        document.RotatePages([0, 1, 2, 3], -1);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(Math.Round(before[i].Width), Math.Round(document.GetPageSize(i).Height));
        }
        document.RotatePages([0, 1, 2, 3], 1);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(Math.Round(before[i].Width), Math.Round(document.GetPageSize(i).Width));
        }
    }

    [Fact]
    public void MovingSeveralPagesKeepsTheirOrder()
    {
        // mixed-rotation: portrait, landscape, portrait, landscape. Moving pages 2 and 4 to the front
        // gives landscape, landscape, portrait, portrait.
        using var document = _engine.Open(RepoPaths.TestFile("mixed-rotation.pdf")).Document!;

        document.MovePages([1, 3], 0);

        var landscape = Enumerable.Range(0, 4).Select(i => document.GetPageSize(i)).Select(s => s.Width > s.Height).ToArray();
        Assert.Equal([true, true, false, false], landscape);
    }

    [Fact]
    public void DeletingAllPagesIsRefused()
    {
        using var document = _engine.Open(RepoPaths.TestFile("blank-pages.pdf")).Document!;

        Assert.Throws<InvalidOperationException>(() => document.DeletePages([0, 1, 2, 3, 4]));
        Assert.Equal(5, document.PageCount);
    }

    [Fact]
    public void OriginalFileStaysUnchangedUntilSaved()
    {
        var path = Path.Combine(_dir, "original.pdf");
        File.Copy(RepoPaths.TestFile("sample-10-pages.pdf"), path);
        var before = File.ReadAllBytes(path);
        using var document = _engine.Open(path).Document!;

        document.DeletePages([0, 1, 2]);
        document.RotatePages([0], 2);

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void SavingAnEditedFileUnderTheSameNameReplacesIt()
    {
        var path = Path.Combine(_dir, "same.pdf");
        File.Copy(RepoPaths.TestFile("sample-10-pages.pdf"), path);
        using var document = _engine.Open(path).Document!;
        document.DeletePages([9]);
        var saver = new DocumentSaver(NullLogger<DocumentSaver>.Instance);

        Assert.True(saver.Save(document.Save, path, sourceToRemove: null));

        using var reopened = _engine.Open(path).Document!;
        Assert.Equal(9, reopened.PageCount);
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void EditedProtectedFileStaysProtected()
    {
        using var document = _engine.Open(RepoPaths.TestFile("sample-password-protected.pdf"), TestFilePassword).Document!;
        document.DeletePages([2]);

        var saved = Save(document, "protected.pdf");

        Assert.Equal(PdfOpenStatus.PasswordRequired, _engine.Open(saved).Status);
        using var reopened = _engine.Open(saved, TestFilePassword).Document!;
        Assert.Equal(2, reopened.PageCount);
    }

    private string Save(IPdfDocument document, string name)
    {
        var path = Path.Combine(_dir, "edited-" + name);
        using (var stream = File.Create(path))
        {
            document.Save(stream);
        }
        return path;
    }
}
