using System.Runtime.InteropServices;

namespace NeroPdf.Tests;

public class PdfiumPackageTests
{
    // Prüft nur, dass das NuGet-Paket die native PDFium-Bibliothek in die Ausgabe legt
    // und sie sich laden lässt. Die eigentliche PDFium-Anbindung folgt in Schritt 1.
    [Fact]
    public void NativePdfiumLibraryCanBeLoaded()
    {
        var loaded = NativeLibrary.TryLoad("pdfium", typeof(PdfiumPackageTests).Assembly, null, out var handle);

        Assert.True(loaded);
        NativeLibrary.Free(handle);
    }
}
