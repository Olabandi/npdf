using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NeroPdf.App.Infrastructure;
using NeroPdf.Core.Platform;

namespace NeroPdf.App.ViewModels;

/// <summary>
/// The hint "Use Nero PDF as the default app for PDF files?". Shown while Nero PDF is not the default;
/// "Ignore" hides it until Nero PDF is closed.
/// </summary>
public sealed class DefaultAppHintViewModel : ObservableObject
{
    private readonly IDefaultPdfApp _defaultApp;
    private readonly ILogger<DefaultAppHintViewModel> _logger;
    private bool _ignored;
    private bool _isVisible;

    public DefaultAppHintViewModel(IDefaultPdfApp defaultApp, ILogger<DefaultAppHintViewModel> logger)
    {
        _defaultApp = defaultApp;
        _logger = logger;
        SetDefaultCommand = new RelayCommand(SetDefault);
        IgnoreCommand = new RelayCommand(Ignore);
    }

    public ICommand SetDefaultCommand { get; }

    public ICommand IgnoreCommand { get; }

    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value);
    }

    /// <summary>Checks again, e.g. when the window becomes active after the user visited the settings.</summary>
    public void Refresh()
    {
        if (!_defaultApp.IsSupported || _ignored)
        {
            IsVisible = false;
            return;
        }
        try
        {
            IsVisible = !_defaultApp.IsDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Default app could not be checked");
            IsVisible = false;
        }
    }

    private void SetDefault()
    {
        try
        {
            _logger.LogInformation("Opening default app settings");
            _defaultApp.OpenSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Default app settings could not be opened");
        }
    }

    private void Ignore()
    {
        _ignored = true;
        IsVisible = false;
        _logger.LogInformation("Default app hint ignored for this session");
    }
}
