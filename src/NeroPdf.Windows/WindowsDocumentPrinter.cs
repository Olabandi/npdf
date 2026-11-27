using System.Runtime.InteropServices;
using NeroPdf.Core.Pdf;
using NeroPdf.Core.Platform;

namespace NeroPdf.Windows;

/// <summary>
/// Printing on Windows: the classic print dialog (printer, page range, copies), then every page is
/// rendered by PDFium as a bitmap and drawn onto the printer page, fitted and centred in the printable area.
/// macOS counterpart: NSPrintOperation, with the Mac build.
/// </summary>
internal sealed class WindowsDocumentPrinter : IDocumentPrinter
{
    public bool IsSupported => true;

    public IPrintJob? AskForPrintJob(nint ownerWindow, int pageCount)
    {
        var dialog = new PrintDlg
        {
            StructSize = Marshal.SizeOf<PrintDlg>(),
            Owner = ownerWindow,
            Flags = ReturnDc | NoSelection | UseDevModeCopiesAndCollate,
            FromPage = 1,
            ToPage = (ushort)Math.Min(pageCount, ushort.MaxValue),
            MinPage = 1,
            MaxPage = (ushort)Math.Min(pageCount, ushort.MaxValue),
            Copies = 1,
        };
        if (!PrintDialog(ref dialog) || dialog.Dc == 0)
        {
            FreeGlobals(dialog);
            return null;
        }
        var (from, to) = (dialog.Flags & PageNums) != 0
            ? (Math.Clamp((int)dialog.FromPage, 1, pageCount), Math.Clamp((int)dialog.ToPage, 1, pageCount))
            : (1, pageCount);
        if (to < from)
        {
            (from, to) = (to, from);
        }
        FreeGlobals(dialog);
        return new Job(dialog.Dc, Enumerable.Range(from - 1, to - from + 1).ToList());
    }

    private static void FreeGlobals(PrintDlg dialog)
    {
        if (dialog.DevMode != 0)
        {
            GlobalFree(dialog.DevMode);
        }
        if (dialog.DevNames != 0)
        {
            GlobalFree(dialog.DevNames);
        }
    }

    private sealed class Job(nint dc, IReadOnlyList<int> pages) : IPrintJob
    {
        // Pages are rendered with at most this resolution; the printer driver scales up.
        private const double MaxRenderDpi = 300;

        private nint _dc = dc;

        public IReadOnlyList<int> Pages { get; } = pages;

        public void Print(IPdfDocument document, string title, IProgress<int>? progress)
        {
            var info = new DocInfo { Size = Marshal.SizeOf<DocInfo>(), DocName = title };
            if (StartDoc(_dc, ref info) <= 0)
            {
                throw new IOException("The printer did not accept the job.");
            }
            var done = 0;
            try
            {
                foreach (var index in Pages)
                {
                    PrintPage(document, index);
                    progress?.Report(++done);
                }
                if (EndDoc(_dc) <= 0)
                {
                    throw new IOException("The printer did not finish the job.");
                }
            }
            catch
            {
                AbortDoc(_dc);
                throw;
            }
        }

        private void PrintPage(IPdfDocument document, int index)
        {
            if (StartPage(_dc) <= 0)
            {
                throw new IOException("The printer did not start a page.");
            }
            // Printable area in device pixels; the page is fitted into it and centred.
            var areaWidth = GetDeviceCaps(_dc, HorzRes);
            var areaHeight = GetDeviceCaps(_dc, VertRes);
            var dpi = Math.Max(1, GetDeviceCaps(_dc, LogPixelsX));
            var size = document.GetPageSize(index);
            var scale = Math.Min(areaWidth / size.Width, areaHeight / size.Height);
            var width = (int)(size.Width * scale);
            var height = (int)(size.Height * scale);
            var x = (areaWidth - width) / 2;
            var y = (areaHeight - height) / 2;

            var renderScale = Math.Min(1, MaxRenderDpi / dpi);
            var bitmap = document.RenderPage(index, Math.Max(1, (int)(width * renderScale)), Math.Max(1, (int)(height * renderScale)));
            var header = new BitmapInfoHeader
            {
                Size = Marshal.SizeOf<BitmapInfoHeader>(),
                Width = bitmap.Width,
                Height = -bitmap.Height, // top-down rows
                Planes = 1,
                BitCount = 32,
            };
            SetStretchBltMode(_dc, HalfTone);
            if (StretchDIBits(_dc, x, y, width, height, 0, 0, bitmap.Width, bitmap.Height, bitmap.Pixels, ref header, DibRgbColors, SrcCopy) == 0)
            {
                throw new IOException("The page could not be drawn on the printer.");
            }
            if (EndPage(_dc) <= 0)
            {
                throw new IOException("The printer did not finish a page.");
            }
        }

        public void Dispose()
        {
            if (_dc != 0)
            {
                DeleteDC(_dc);
                _dc = 0;
            }
        }
    }

    // ---- Win32 --------------------------------------------------------------------------------

    private const int ReturnDc = 0x100;
    private const int NoSelection = 0x4;
    private const int PageNums = 0x2;
    private const int UseDevModeCopiesAndCollate = 0x40000;
    private const int HorzRes = 8;
    private const int VertRes = 10;
    private const int LogPixelsX = 88;
    private const int HalfTone = 4;
    private const uint DibRgbColors = 0;
    private const uint SrcCopy = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrintDlg
    {
        public int StructSize;
        public nint Owner;
        public nint DevMode;
        public nint DevNames;
        public nint Dc;
        public int Flags;
        public ushort FromPage;
        public ushort ToPage;
        public ushort MinPage;
        public ushort MaxPage;
        public ushort Copies;
        public nint Instance;
        public nint CustomData;
        public nint PrintHook;
        public nint SetupHook;
        public nint PrintTemplateName;
        public nint SetupTemplateName;
        public nint PrintTemplate;
        public nint SetupTemplate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        public int Size;
        public string DocName;
        public string? Output;
        public string? DataType;
        public int Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("comdlg32.dll", EntryPoint = "PrintDlgW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintDialog(ref PrintDlg dialog);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalFree(nint memory);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int StartDoc(nint dc, ref DocInfo info);

    [DllImport("gdi32.dll")]
    private static extern int EndDoc(nint dc);

    [DllImport("gdi32.dll")]
    private static extern int AbortDoc(nint dc);

    [DllImport("gdi32.dll")]
    private static extern int StartPage(nint dc);

    [DllImport("gdi32.dll")]
    private static extern int EndPage(nint dc);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(nint dc, int index);

    [DllImport("gdi32.dll")]
    private static extern int SetStretchBltMode(nint dc, int mode);

    [DllImport("gdi32.dll")]
    private static extern int StretchDIBits(nint dc, int x, int y, int width, int height, int sourceX, int sourceY,
        int sourceWidth, int sourceHeight, byte[] bits, ref BitmapInfoHeader info, uint usage, uint rop);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint dc);
}
