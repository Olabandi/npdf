using Microsoft.Extensions.Logging.Abstractions;
using NeroPdf.Core.Documents;
using NeroPdf.Core.Pdf;
using NeroPdf.Core.Pdf.Pdfium;
using NeroPdf.Core.Platform;
using NeroPdf.Core.Settings;

namespace NeroPdf.Tests;

/// <summary>Saving (move, copy, same place), print inbox and settings.</summary>
public sealed class SaveAndInboxTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("NeroPdfTest_").FullName;
    private readonly DocumentSaver _saver = new(NullLogger<DocumentSaver>.Instance);
    private readonly PdfiumEngine _engine = new(NullLogger<PdfiumEngine>.Instance);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("Microsoft Word - Brief an Bank.docx", "Brief an Bank")]
    [InlineData("Rechnung 2026.pdf", "Rechnung 2026")]
    [InlineData("https://example.com/a?b", "httpsexample.comab")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    [InlineData("Report: Q3 / final", "Report Q3  final")]
    public void PrintJobTitleBecomesFileName(string? title, string expected)
    {
        Assert.Equal(expected, PrintFileName.Clean(title));
    }

    [Fact]
    public void InboxFileNameKeepsTitleAndIsUnique()
    {
        var time = new DateTime(2026, 11, 25, 22, 30, 0);
        var a = PrintFileName.Create("Microsoft Word - Brief.docx", time, Guid.NewGuid());
        var b = PrintFileName.Create("Microsoft Word - Brief.docx", time, Guid.NewGuid());

        Assert.NotEqual(a, b);
        Assert.Equal("Brief", PrintFileName.TitleOf(a + ".pdf"));
        Assert.Null(PrintFileName.TitleOf(PrintFileName.Create(null, time, Guid.NewGuid()) + ".pdf"));
    }

    [Fact]
    public void SavingPrintedDocumentMovesIt()
    {
        var printed = CopyTestFile("sample-10-pages.pdf", "printed.pdf");
        var target = Path.Combine(_dir, "saved.pdf");

        Assert.True(_saver.Save(printed, target, move: true));

        Assert.False(File.Exists(printed));
        AssertOpensWithPages(target, 10);
    }

    [Fact]
    public void SaveAsKeepsTheOriginal()
    {
        var original = CopyTestFile("sample-10-pages.pdf", "original.pdf");
        var target = Path.Combine(_dir, "copy.pdf");

        Assert.True(_saver.Save(original, target, move: false));

        AssertOpensWithPages(original, 10);
        AssertOpensWithPages(target, 10);
    }

    [Fact]
    public void SavingUnderTheSameNameWhileOpenWorks()
    {
        var path = CopyTestFile("sample-10-pages.pdf", "same.pdf");
        using var open = _engine.Open(path).Document!;

        Assert.True(_saver.Save(path, path, move: false));

        Assert.Equal(10, open.PageCount);
        AssertOpensWithPages(path, 10);
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void OverwritingAnExistingFileWorks()
    {
        var source = CopyTestFile("sample-10-pages.pdf", "source.pdf");
        var target = CopyTestFile("sample-1-page.pdf", "target.pdf");

        Assert.True(_saver.Save(source, target, move: false));

        AssertOpensWithPages(target, 10);
    }

    [Fact]
    public void FailedSaveLeavesSourceAndNoTemporaryFile()
    {
        var printed = CopyTestFile("sample-10-pages.pdf", "printed.pdf");
        var target = Path.Combine(_dir, "missing-folder", "saved.pdf");

        Assert.False(_saver.Save(printed, target, move: true));

        AssertOpensWithPages(printed, 10);
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void PasswordProtectedFileStaysProtectedWhenSavedAs()
    {
        var original = CopyTestFile("sample-password-protected.pdf", "protected.pdf");
        var target = Path.Combine(_dir, "protected-copy.pdf");

        Assert.True(_saver.Save(original, target, move: false));

        Assert.Equal(PdfOpenStatus.PasswordRequired, _engine.Open(target).Status);
    }

    [Fact]
    public void InboxHandsEachCompleteFileToOneInstanceOnly()
    {
        var incoming = Directory.CreateDirectory(Path.Combine(_dir, PrintFolders.Incoming)).FullName;
        File.Copy(RepoPaths.TestFile("sample-1-page.pdf"), Path.Combine(incoming, "a.pdf"));
        File.Copy(RepoPaths.TestFile("sample-1-page.pdf"), Path.Combine(incoming, "b" + PrintFolders.PartialExtension));
        var first = new FolderPrintInbox(_dir);
        var second = new FolderPrintInbox(_dir);

        var claimed = first.TryClaimNext();

        Assert.NotNull(claimed);
        Assert.Equal(Path.Combine(_dir, PrintFolders.Opened, "a.pdf"), claimed);
        Assert.Null(second.TryClaimNext());
    }

    [Fact]
    public void CleanUpRemovesOnlyOldUnsavedPrints()
    {
        var opened = Directory.CreateDirectory(Path.Combine(_dir, PrintFolders.Opened)).FullName;
        var old = Path.Combine(opened, "old.pdf");
        var recent = Path.Combine(opened, "recent.pdf");
        File.WriteAllText(old, "x");
        File.WriteAllText(recent, "x");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-8));

        new FolderPrintInbox(_dir).CleanUp(TimeSpan.FromDays(7));

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void UserSettingsOverlayDefaults()
    {
        var defaults = Path.Combine(_dir, "defaults.json");
        var user = Path.Combine(_dir, "user", "settings.json");
        File.WriteAllText(defaults, """{ "Theme": "Light", "UnsavedPrintRetentionDays": 7 }""");
        Directory.CreateDirectory(Path.GetDirectoryName(user)!);
        File.WriteAllText(user, """{ "Theme": "Dark" }""");
        var store = new SettingsStore(defaults, user, NullLogger<SettingsStore>.Instance);

        var settings = store.Load();

        Assert.Equal("Dark", settings.Theme);
        Assert.Equal(7, settings.UnsavedPrintRetentionDays);
    }

    private string CopyTestFile(string name, string as_)
    {
        var path = Path.Combine(_dir, as_);
        File.Copy(RepoPaths.TestFile(name), path);
        return path;
    }

    private void AssertOpensWithPages(string path, int pages)
    {
        using var document = _engine.Open(path).Document;
        Assert.NotNull(document);
        Assert.Equal(pages, document.PageCount);
    }
}
