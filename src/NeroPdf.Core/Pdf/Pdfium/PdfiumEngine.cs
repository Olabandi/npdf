using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace NeroPdf.Core.Pdf.Pdfium;

/// <summary>PDF engine based on PDFium.</summary>
public sealed class PdfiumEngine : IPdfEngine
{
    /// <summary>PDFium is not thread-safe; all native calls are serialized on this lock.</summary>
    internal static readonly object Sync = new();

    private static bool _initialized;
    private readonly ILogger<PdfiumEngine> _logger;

    public PdfiumEngine(ILogger<PdfiumEngine> logger)
    {
        _logger = logger;
        lock (Sync)
        {
            if (!_initialized)
            {
                PdfiumNative.InitLibrary();
                _initialized = true;
                _logger.LogDebug("PDFium initialized");
            }
        }
    }

    public PdfOpenResult Open(string path, string? password = null)
    {
        var watch = Stopwatch.StartNew();
        byte[] content;
        try
        {
            content = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _logger.LogInformation("Open failed: file not found");
            return PdfOpenResult.Failure(PdfOpenStatus.NotFound);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Open failed: file could not be read");
            return PdfOpenResult.Failure(PdfOpenStatus.Failed);
        }

        var buffer = new NativeBuffer(content);
        lock (Sync)
        {
            var handle = PdfiumNative.LoadMemDocument64(buffer.Pointer, (nuint)buffer.Length, password);
            if (handle == IntPtr.Zero)
            {
                var error = PdfiumNative.GetLastError();
                buffer.Dispose();
                var status = MapError(error, password);
                _logger.LogInformation("Open failed: PDFium error {Error} -> {Status}", error, status);
                return PdfOpenResult.Failure(status);
            }

            var document = new PdfiumDocument(handle, buffer);
            _logger.LogInformation("Document opened: {Pages} pages, {Bytes} bytes, {Ms} ms",
                document.PageCount, content.Length, watch.ElapsedMilliseconds);
            return new PdfOpenResult(PdfOpenStatus.Ok, document);
        }
    }

    private static PdfOpenStatus MapError(PdfiumNative.ErrorCode error, string? password) => error switch
    {
        PdfiumNative.ErrorCode.File => PdfOpenStatus.NotFound,
        PdfiumNative.ErrorCode.Format => PdfOpenStatus.Damaged,
        PdfiumNative.ErrorCode.Password => password is null ? PdfOpenStatus.PasswordRequired : PdfOpenStatus.WrongPassword,
        PdfiumNative.ErrorCode.Security => PdfOpenStatus.Unsupported,
        _ => PdfOpenStatus.Failed,
    };

    /// <summary>Unmanaged copy of the file; PDFium reads from it for the lifetime of the document.</summary>
    internal sealed class NativeBuffer : IDisposable
    {
        public NativeBuffer(byte[] content)
        {
            Length = content.Length;
            Pointer = Marshal.AllocHGlobal(Math.Max(1, content.Length));
            Marshal.Copy(content, 0, Pointer, content.Length);
        }

        public IntPtr Pointer { get; private set; }

        public int Length { get; }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Pointer);
                Pointer = IntPtr.Zero;
            }
        }
    }
}
