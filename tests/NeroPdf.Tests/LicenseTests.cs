using System.Xml.Linq;

namespace NeroPdf.Tests;

/// <summary>The licence texts shipped with Nero PDF must match the components in use.</summary>
public class LicenseTests
{
    private static string Licenses => Path.Combine(RepoPaths.Root, "licenses");

    [Fact]
    public void PdfiumLicensesMatchThePackageVersion()
    {
        var version = XDocument.Load(Path.Combine(RepoPaths.Root, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Single(e => (string?)e.Attribute("Include") == "bblanchon.PDFium.Win32")
            .Attribute("Version")!.Value;

        var recorded = File.ReadAllText(Path.Combine(Licenses, "pdfium", "PACKAGE-VERSION.txt")).Trim();
        var notices = File.ReadAllText(Path.Combine(Licenses, "THIRD-PARTY-NOTICES.txt"));

        Assert.Equal("bblanchon.PDFium " + version, recorded);
        Assert.Contains(version, notices);
        Assert.True(File.Exists(Path.Combine(Licenses, "pdfium", "pdfium.txt")));
    }

    [Fact]
    public void EveryLicenceFolderIsListedInTheNotices()
    {
        var notices = File.ReadAllText(Path.Combine(Licenses, "THIRD-PARTY-NOTICES.txt"));
        foreach (var folder in Directory.GetDirectories(Licenses).Select(Path.GetFileName))
        {
            Assert.Contains(folder + "\\", notices);
        }
    }
}
