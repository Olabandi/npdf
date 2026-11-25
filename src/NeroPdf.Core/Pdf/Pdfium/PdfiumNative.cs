using System.Runtime.InteropServices;

namespace NeroPdf.Core.Pdf.Pdfium;

/// <summary>
/// The PDFium functions Nero PDF uses. The native library comes from the bblanchon.PDFium packages.
/// PDFium is not thread-safe: every call goes through <see cref="PdfiumEngine.Sync"/>.
/// </summary>
internal static partial class PdfiumNative
{
    private const string Library = "pdfium";

    public const int BitmapFormatBgra = 4;
    public const int RenderAnnotations = 0x01;

    public enum ErrorCode : uint
    {
        Success = 0,
        Unknown = 1,
        File = 2,
        Format = 3,
        Password = 4,
        Security = 5,
        Page = 6,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SizeF
    {
        public float Width;
        public float Height;
    }

    [LibraryImport(Library, EntryPoint = "FPDF_InitLibrary")]
    public static partial void InitLibrary();

    [LibraryImport(Library, EntryPoint = "FPDF_LoadMemDocument64", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr LoadMemDocument64(IntPtr data, nuint size, string? password);

    [LibraryImport(Library, EntryPoint = "FPDF_GetLastError")]
    public static partial ErrorCode GetLastError();

    [LibraryImport(Library, EntryPoint = "FPDF_CloseDocument")]
    public static partial void CloseDocument(IntPtr document);

    [LibraryImport(Library, EntryPoint = "FPDF_GetPageCount")]
    public static partial int GetPageCount(IntPtr document);

    [LibraryImport(Library, EntryPoint = "FPDF_GetPageSizeByIndexF")]
    public static partial int GetPageSizeByIndexF(IntPtr document, int index, out SizeF size);

    [LibraryImport(Library, EntryPoint = "FPDF_LoadPage")]
    public static partial IntPtr LoadPage(IntPtr document, int index);

    [LibraryImport(Library, EntryPoint = "FPDF_ClosePage")]
    public static partial void ClosePage(IntPtr page);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_CreateEx")]
    public static partial IntPtr BitmapCreateEx(int width, int height, int format, IntPtr firstScan, int stride);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_FillRect")]
    public static partial int BitmapFillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

    [LibraryImport(Library, EntryPoint = "FPDFBitmap_Destroy")]
    public static partial void BitmapDestroy(IntPtr bitmap);

    [LibraryImport(Library, EntryPoint = "FPDFPage_GetRotation")]
    public static partial int PageGetRotation(IntPtr page);

    [LibraryImport(Library, EntryPoint = "FPDFPage_SetRotation")]
    public static partial void PageSetRotation(IntPtr page, int rotate);

    [LibraryImport(Library, EntryPoint = "FPDFPage_Delete")]
    public static partial void PageDelete(IntPtr document, int index);

    [LibraryImport(Library, EntryPoint = "FPDF_MovePages")]
    public static unsafe partial int MovePages(IntPtr document, int* pageIndices, nuint length, int destinationIndex);

    /// <summary>Full rewrite, no incremental update.</summary>
    public const uint SaveNoIncremental = 2;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct FileWrite
    {
        public int Version;
        // int WriteBlock(FPDF_FILEWRITE* self, const void* data, unsigned long size); "unsigned long" is 32 bit on Windows.
        public delegate* unmanaged[Cdecl]<FileWrite*, byte*, CULong, int> WriteBlock;
    }

    [LibraryImport(Library, EntryPoint = "FPDF_SaveAsCopy")]
    public static unsafe partial int SaveAsCopy(IntPtr document, FileWrite* fileWrite, uint flags);

    [LibraryImport(Library, EntryPoint = "FPDF_RenderPageBitmap")]
    public static partial void RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY,
        int sizeX, int sizeY, int rotate, int flags);
}
