using Avalonia.Controls;
using Avalonia.Input;
using NeroPdf.App.ViewModels;
using NeroPdf.Core.Platform;

namespace NeroPdf.App.Dialogs;

/// <summary>Asks for scanner, paper source and colour before scanning. Closes with a <see cref="ScanRequest"/> or null.</summary>
public partial class ScanDialog : Window
{
    private readonly IReadOnlyList<ScannerDevice> _devices = [];

    public ScanDialog()
    {
        InitializeComponent();
    }

    public ScanDialog(IReadOnlyList<ScannerDevice> devices) : this()
    {
        _devices = devices;
        Scanners.ItemsSource = devices.Select(d => d.Name).ToList();
        Scanners.SelectedIndex = 0;
        ScannerGroup.IsVisible = devices.Count > 1;
        Scanners.SelectionChanged += (_, _) => ShowSources();
        ShowSources();

        Start.Click += (_, _) => Close(CreateRequest());
        Cancel.Click += (_, _) => Close(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
            }
        };
    }

    private ScannerDevice? Selected =>
        Scanners.SelectedIndex >= 0 && Scanners.SelectedIndex < _devices.Count ? _devices[Scanners.SelectedIndex] : null;

    /// <summary>The paper source is only asked when the scanner has both; the feeder is preselected then.</summary>
    private void ShowSources()
    {
        var device = Selected;
        ScannerName.Text = device?.Name;
        var both =device is { HasFeeder: true, HasFlatbed: true };
        SourceGroup.IsVisible = both;
        Feeder.IsChecked = device?.HasFeeder == true;
        Flatbed.IsChecked = device?.HasFeeder != true;
    }

    private ScanRequest? CreateRequest() => Selected is { } device
        ? new ScanRequest(device,
            Feeder.IsChecked == true ? ScanSource.Feeder : ScanSource.Flatbed,
            GrayOption.IsChecked == true ? ScanColor.Gray : ScanColor.Color)
        : null;
}
