using Avalonia;
using Avalonia.Controls;
using NeroPdf.App.ViewModels;

namespace NeroPdf.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (Application.Current is { } application)
        {
            ContentScope.RequestedThemeVariant = application.RequestedThemeVariant;
            application.ActualThemeVariantChanged += (_, _) => ContentScope.RequestedThemeVariant = application.RequestedThemeVariant;
        }
        PageScroller.ScrollChanged += (_, _) => UpdateCurrentPage();
        PageScroller.SizeChanged += (_, _) => UpdateCurrentPage();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnPageShown(object? sender, int index) => ViewModel?.NotifyPageShown(index);

    /// <summary>The current page is the one at the vertical middle of the visible area.</summary>
    private void UpdateCurrentPage()
    {
        if (ViewModel is not { HasPages: true } viewModel)
        {
            return;
        }
        var middle = PageScroller.Viewport.Height / 2;
        foreach (var container in PageList.GetRealizedContainers())
        {
            if (container.TranslatePoint(default, PageScroller) is not { } top)
            {
                continue;
            }
            if (top.Y <= middle && top.Y + container.Bounds.Height >= middle)
            {
                viewModel.CurrentPage = PageList.IndexFromContainer(container) + 1;
                return;
            }
        }
    }
}
