using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Infrastructure;
using NeroPdf.App.ViewModels;
using NeroPdf.Core.Pdf.Pdfium;
using NeroPdf.Core.Platform;
using NeroPdf.Core.Settings;

namespace NeroPdf.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && AppHost.Platform is { } platform)
        {
            desktop.MainWindow = CreateMainWindow(platform);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private MainWindow CreateMainWindow(PlatformServices platform)
    {
        var loggers = AppHost.LoggerFactory;
        var logger = loggers.CreateLogger<App>();
        var baseDirectory = AppContext.BaseDirectory;

        var settingsStore = new SettingsStore(Path.Combine(baseDirectory, "Config", "defaults.json"),
            Path.Combine(platform.Paths.DataDirectory, "settings.json"), loggers.CreateLogger<SettingsStore>());
        var settings = settingsStore.Load();

        var themes = new ThemeManager(this, baseDirectory, loggers.CreateLogger<ThemeManager>());
        themes.ApplyLayout(platform.HeaderInsets);
        themes.ApplyTheme(settings.Theme);

        var viewModel = new MainViewModel(
            new PdfiumEngine(loggers.CreateLogger<PdfiumEngine>()),
            settings.Theme == ThemeManager.Dark,
            dark =>
            {
                settings.Theme = dark ? ThemeManager.Dark : ThemeManager.Light;
                themes.ApplyTheme(settings.Theme);
                settingsStore.Save(settings);
            },
            TimeSpan.FromMilliseconds(themes.LayoutValue("ProgressDelayMilliseconds", 400)),
            loggers.CreateLogger<MainViewModel>());
        viewModel.FirstPageShown += (_, _) =>
            logger.LogInformation("Startup: first page shown after {Ms} ms", AppHost.StartTime.ElapsedMilliseconds);

        var window = new MainWindow { DataContext = viewModel };
        if (CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft)
        {
            window.FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        }
        window.Opened += async (_, _) => await OpenInitialDocumentAsync(viewModel, platform, logger);
        window.Closed += (_, _) => viewModel.Dispose();
        return window;
    }

    private static async Task OpenInitialDocumentAsync(MainViewModel viewModel, PlatformServices platform, ILogger logger)
    {
        if (AppHost.FileArgument is { } file)
        {
            await viewModel.OpenAsync(file, showFileName: true);
            return;
        }
        var printed = platform.PrintInbox.TryClaimNext();
        if (printed is not null)
        {
            await viewModel.OpenAsync(printed, showFileName: false);
            return;
        }
        logger.LogInformation("Started without a document");
    }
}
