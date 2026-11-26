using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NeroPdf.Core.Logging;
using NeroPdf.Core.Platform;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Graphics.Printing.Workflow;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT;

namespace NeroPdf.Windows.PrintTasks
{
    /// <summary>
    /// Called by Windows when something is printed on the "Nero PDF" printer. Converts the job to PDF,
    /// puts it into the print inbox (package temp folder) and starts Nero PDF, which claims the file.
    /// </summary>
    public sealed class VirtualPrinterTask : IBackgroundTask
    {
        /// <summary>Parameter group of the full trust process in the package manifest.</summary>
        private const string ViewerParameterGroup = "Print";

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var loggers = TaskLogging.Create();
            var logger = loggers.CreateLogger<VirtualPrinterTask>();
            var deferral = taskInstance.GetDeferral();
            var completed = 0;
            void Complete()
            {
                if (Interlocked.Exchange(ref completed, 1) == 0)
                {
                    deferral.Complete();
                    loggers.Dispose();
                }
            }
            taskInstance.Canceled += (_, reason) =>
            {
                logger.LogDebug("Print task canceled: {Reason}", reason);
                Complete();
            };

            if (TaskLogging.TriggerAs<PrintWorkflowVirtualPrinterTriggerDetails>(taskInstance.TriggerDetails) is not { } details)
            {
                logger.LogError("Print task started with unexpected trigger");
                Complete();
                return;
            }

            var session = details.VirtualPrinterSession;
            session.VirtualPrinterDataAvailable += async (_, args) =>
            {
                var status = PrintWorkflowSubmittedStatus.Failed;
                try
                {
                    await WritePdfAsync(args, logger);
                    status = PrintWorkflowSubmittedStatus.Succeeded;
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync(ViewerParameterGroup);
                    logger.LogInformation("Nero PDF started for the print job");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Print job could not be processed");
                }
                finally
                {
                    args.CompleteJob(status);
                    Complete();
                }
            };
            session.Start();
        }

        private static async Task WritePdfAsync(PrintWorkflowVirtualPrinterDataAvailableEventArgs args, ILogger logger)
        {
            var watch = Stopwatch.StartNew();
            var source = args.SourceContent;
            logger.LogInformation("Print job received, format {Format}", source.ContentType);

            var incoming = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, PrintFolders.Incoming);
            Directory.CreateDirectory(incoming);
            var name = NeroPdf.Core.Documents.PrintFileName.Create(args.Configuration.JobTitle, DateTime.Now, Guid.NewGuid());
            var partial = Path.Combine(incoming, name + PrintFolders.PartialExtension);

            using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var output = file.AsOutputStream())
            {
                if (source.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    await RandomAccessStream.CopyAsync(source.GetInputStream(), output);
                }
                else
                {
                    var converter = args.GetPdlConverter(PrintWorkflowPdlConversionType.XpsToPdf);
                    await converter.ConvertPdlAsync(args.GetJobPrintTicket(), source.GetInputStream(), output);
                }
                await output.FlushAsync();
            }

            // Only complete files carry the .pdf extension, so Nero PDF never opens a half-written file.
            File.Move(partial, Path.Combine(incoming, name + ".pdf"));
            logger.LogInformation("Print job written as PDF in {Ms} ms", watch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Required by Windows for virtual printers (see cube-soft/cube.psa.samples). Nero PDF has no
    /// printer options of its own, so every print ticket is accepted as it is.
    /// </summary>
    public sealed class PrintSupportExtensionTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();
            var loggers = TaskLogging.Create();
            var logger = loggers.CreateLogger<PrintSupportExtensionTask>();
            try
            {
                logger.LogDebug("Print support session starting");
                taskInstance.Canceled += (_, _) => Safe(() =>
                {
                    deferral.Complete();
                    loggers.Dispose();
                });
                if (TaskLogging.TriggerAs<global::Windows.Graphics.Printing.PrintSupport.PrintSupportExtensionTriggerDetails>(taskInstance.TriggerDetails) is not { } details
                    || details.Session is null)
                {
                    logger.LogError("Print support task started with unexpected trigger");
                    deferral.Complete();
                    return;
                }
                details.Session.PrintTicketValidationRequested += (_, e) => Safe(() =>
                {
                    using (e.GetDeferral())
                    {
                        e.SetPrintTicketValidationStatus(global::Windows.Graphics.Printing.PrintSupport.WorkflowPrintTicketValidationStatus.Resolved);
                    }
                });
                details.Session.Start();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Print support session failed");
                Safe(deferral.Complete);
            }
        }

        // The session can be torn down by Windows at any time; exceptions must not crash the host process.
        private static void Safe(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
            }
        }
    }

    internal static class TaskLogging
    {
        /// <summary>
        /// The trigger details arrive as a plain WinRT object. A C# type check needs type information that
        /// Native AOT does not keep, so the object is asked for the interface directly.
        /// </summary>
        public static T? TriggerAs<T>(object? details) where T : class
        {
            if (details is T typed)
            {
                return typed;
            }
            try
            {
                return details?.As<T>();
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

        public static ILoggerFactory Create() =>
            AppLogging.Create(Path.Combine(ApplicationData.Current.LocalFolder.Path, "Logs"), verbose: false);
    }
}
