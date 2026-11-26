using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Infrastructure;
using NeroPdf.App.ViewModels;
using NeroPdf.Core.Documents;
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
        CleanUpUnsavedPrints(platform, settings, logger);

        var themes = new ThemeManager(this, baseDirectory, loggers.CreateLogger<ThemeManager>());
        themes.ApplyLayout(platform.HeaderInsets);
        themes.ApplyTheme(settings.Theme);
        var sidebarSizer = new SidebarSizer(this, themes, settings.SidebarWidth, width =>
        {
            settings.SidebarWidth = width;
            settingsStore.Save(settings);
        });

        var viewModel = new MainViewModel(
            new PdfiumEngine(loggers.CreateLogger<PdfiumEngine>()),
            new DocumentSaver(loggers.CreateLogger<DocumentSaver>()),
            new PageOverviewViewModel(loggers.CreateLogger<PageOverviewViewModel>()),
            settings.Theme == ThemeManager.Dark,
            dark =>
            {
                settings.Theme = dark ? ThemeManager.Dark : ThemeManager.Light;
                themes.ApplyTheme(settings.Theme);
                settingsStore.Save(settings);
            },
            TimeSpan.FromMilliseconds(themes.LayoutValue("ProgressDelayMilliseconds", 400)),
            loggers.CreateLogger<MainViewModel>());
        viewModel.IsSidebarExpanded = !settings.SidebarCollapsed;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsSidebarExpanded))
            {
                settings.SidebarCollapsed = !viewModel.IsSidebarExpanded;
                settingsStore.Save(settings);
            }
        };
        viewModel.FirstPageShown += (_, _) =>
            logger.LogInformation("Startup: first page shown after {Ms} ms", AppHost.StartTime.ElapsedMilliseconds);

        var defaultAppHint = new DefaultAppHintViewModel(platform.DefaultPdfApp, loggers.CreateLogger<DefaultAppHintViewModel>());
        viewModel.DefaultAppHint = defaultAppHint;

        var promoLinks = new PromoLinksViewModel(Path.Combine(baseDirectory, "Config", "links.json"),
            loggers.CreateLogger<PromoLinksViewModel>());
        viewModel.PromoLinks = promoLinks;

        var window = new MainWindow { DataContext = viewModel, SidebarSizer = sidebarSizer };
        promoLinks.Launcher = uri => window.Launcher.LaunchUriAsync(uri);
        window.Activated += (_, _) => defaultAppHint.Refresh();
        if (CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft)
        {
            window.FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        }
        window.Opened += async (_, _) => await OpenInitialDocumentAsync(viewModel, platform, logger);
        window.Closed += (_, _) => viewModel.Dispose();
        return window;
    }

    /// <summary>Printed documents that were never saved are removed after the configured number of days.</summary>
    private static void CleanUpUnsavedPrints(PlatformServices platform, AppSettings settings, ILogger logger)
    {
        if (settings.UnsavedPrintRetentionDays <= 0)
        {
            return;
        }
        try
        {
            platform.PrintInbox.CleanUp(TimeSpan.FromDays(settings.UnsavedPrintRetentionDays));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Old printed documents could not be cleaned up");
        }
    }

    private static async Task OpenInitialDocumentAsync(MainViewModel viewModel, PlatformServices platform, ILogger logger)
    {
        if (AppHost.FileArgument is { } file)
        {
            await viewModel.OpenAsync(file, printed: false);
            return;
        }
        var printed = platform.PrintInbox.TryClaimNext();
        if (printed is not null)
        {
            await viewModel.OpenAsync(printed, printed: true);
            return;
        }
        logger.LogInformation("Started without a document");
    }
}
