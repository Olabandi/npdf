using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NeroPdf.App.ViewModels;

namespace NeroPdf.App;

public partial class MainWindow : Window
{
    // Distance the pointer has to move before a press on a thumbnail becomes a drag.
    private const double DragThreshold = 6;
    // Height of the zone at the top and bottom of the overview that scrolls while dragging.
    private const double AutoScrollZone = 40;

    private bool _closeConfirmed;
    private PageViewModel? _pressedPage;
    private Point _pressPoint;
    private bool _dragging;
    private double _splitterStartX = double.NaN;
    private double _splitterStartWidth;

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
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(KeyDownEvent, OnPageKeyDown, RoutingStrategies.Tunnel);
        ThumbnailList.AddHandler(PointerPressedEvent, OnThumbnailPressed, RoutingStrategies.Tunnel);
        ThumbnailList.AddHandler(PointerMovedEvent, OnThumbnailMoved, RoutingStrategies.Tunnel);
        ThumbnailList.AddHandler(PointerReleasedEvent, OnThumbnailReleased, RoutingStrategies.Tunnel);
        ThumbnailList.AddHandler(PointerCaptureLostEvent, (_, _) => _ = EndDragAsync(drop: false));
        Closing += OnClosing;
        SidebarSplitter.PointerPressed += OnSplitterPressed;
        SidebarSplitter.PointerMoved += OnSplitterMoved;
        SidebarSplitter.PointerReleased += OnSplitterReleased;
    }

    /// <summary>Width of the page overview; set by the app.</summary>
    public Infrastructure.SidebarSizer? SidebarSizer { get; init; }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private PageOverviewViewModel? Overview => ViewModel?.Overview;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (ViewModel is { } viewModel)
        {
            viewModel.Dialogs = new Dialogs.Dialogs(this);
            viewModel.Overview.ScrollToPageRequested += (_, index) => ScrollToPage(index);
            viewModel.Overview.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PageOverviewViewModel.CurrentPage))
                {
                    ThumbnailList.ScrollIntoView(viewModel.Overview.CurrentPage - 1);
                }
            };
            AddShortcuts(viewModel);
        }
    }

    /// <summary>Ctrl+O / Ctrl+S / Ctrl+Shift+S, with the platform's command key (Cmd on macOS).</summary>
    private void AddShortcuts(MainViewModel viewModel)
    {
        var command = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        KeyBindings.Clear();
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.O, command), Command = viewModel.OpenCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, command), Command = viewModel.SaveCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, command | KeyModifiers.Shift), Command = viewModel.SaveAsCommand });
    }

    /// <summary>Unsaved printouts and unsaved changes are never closed without asking.</summary>
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || ViewModel is not { HasUnsavedChanges: true } viewModel)
        {
            return;
        }
        e.Cancel = true;
        if (await viewModel.ConfirmCloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    // ---- Files dropped from outside --------------------------------------------------------

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = FirstPdf(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (FirstPdf(e) is { } path && ViewModel is { } viewModel)
        {
            e.Handled = true;
            await viewModel.OpenFileAsync(path);
        }
    }

    private static string? FirstPdf(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?
            .Select(item => item.TryGetLocalPath())
            .FirstOrDefault(path => path is not null && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

    // ---- Keyboard ---------------------------------------------------------------------------

    /// <summary>Page Up / Page Down, Home / End page by page; Delete removes the marked pages.</summary>
    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { HasPages: true } viewModel || e.KeyModifiers != KeyModifiers.None || e.Source is TextBox)
        {
            return;
        }
        var overview = viewModel.Overview;
        switch (e.Key)
        {
            case Key.PageDown:
                overview.NextPage();
                break;
            case Key.PageUp:
                overview.PreviousPage();
                break;
            case Key.Home:
                overview.FirstPage();
                break;
            case Key.End:
                overview.LastPage();
                break;
            case Key.Delete when overview.CanDelete:
                overview.DeleteCommand.Execute(null);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // ---- Page overview: marking, toolbar, drag and drop -------------------------------------

    private void OnThumbnailPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Overview is not { } overview || PageAt(e.Source) is not { } page || IsInside<Button>(e.Source))
        {
            return;
        }
        var point = e.GetCurrentPoint(ThumbnailList);
        if (point.Properties.IsRightButtonPressed)
        {
            overview.EnsureSelected(page.Index);
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var command = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            overview.SelectRange(page.Index);
        }
        else if (e.KeyModifiers.HasFlag(command))
        {
            overview.Toggle(page.Index);
        }
        else
        {
            if (!page.IsSelected)
            {
                overview.SelectOnly(page.Index);
            }
            else
            {
                overview.GoToPage(page.Index);
            }
            _pressedPage = page;
            _pressPoint = e.GetPosition(this);
            e.Pointer.Capture(ThumbnailList);
        }
        e.Handled = true;
    }

    private void OnThumbnailMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedPage is null || Overview is not { } overview)
        {
            return;
        }
        var position = e.GetPosition(this);
        if (!_dragging)
        {
            if (Math.Abs(position.X - _pressPoint.X) + Math.Abs(position.Y - _pressPoint.Y) < DragThreshold)
            {
                return;
            }
            _dragging = true;
            var count = overview.BeginDrag(_pressedPage.Index);
            DragGhostPage.Page = _pressedPage;
            DragGhostCount.Text = count.ToString(System.Globalization.CultureInfo.CurrentCulture);
            DragGhost.IsVisible = true;
        }

        if (DragGhost.Parent is Visual canvas && e.GetPosition(canvas) is var local)
        {
            Canvas.SetLeft(DragGhost, local.X - DragGhost.Bounds.Width / 2);
            Canvas.SetTop(DragGhost, local.Y - 20);
        }
        overview.UpdateDropIndex(DropIndexAt(e));
        AutoScroll(e.GetPosition(ThumbnailScroller).Y);
    }

    private async void OnThumbnailReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Only presses on a thumbnail itself; buttons (check box, toolbar) handle their own release.
        if (_pressedPage is null)
        {
            return;
        }
        // Finish the drop before releasing the pointer: releasing it raises "capture lost", which cancels.
        var drop = EndDragAsync(drop: true);
        _pressedPage = null;
        e.Pointer.Capture(null);
        e.Handled = true;
        await drop;
    }

    private async Task EndDragAsync(bool drop)
    {
        if (!_dragging || Overview is not { } overview)
        {
            _pressedPage = null;
            return;
        }
        _dragging = false;
        _pressedPage = null;
        DragGhost.IsVisible = false;
        DragGhostPage.Page = null;
        await overview.EndDragAsync(drop);
    }

    /// <summary>Insertion index under the pointer: before the page whose middle is below the pointer.</summary>
    private int DropIndexAt(PointerEventArgs e)
    {
        var pages = Overview!.Pages;
        var y = e.GetPosition(ThumbnailList).Y;
        foreach (var container in ThumbnailList.GetRealizedContainers().OrderBy(c => ThumbnailList.IndexFromContainer(c)))
        {
            if (container.TranslatePoint(default, ThumbnailList) is not { } top)
            {
                continue;
            }
            var index = ThumbnailList.IndexFromContainer(container);
            var gap = pages[index].HasGapBefore ? GapHeight() : 0;
            if (y < top.Y + gap + (container.Bounds.Height - gap) / 2)
            {
                return index;
            }
        }
        return pages.Count;
    }

    private double GapHeight() =>
        Application.Current?.Resources.TryGetResource("Layout.DropGapHeight", null, out var value) == true && value is double height ? height : 0;

    private void AutoScroll(double y)
    {
        var height = ThumbnailScroller.Bounds.Height;
        var step = y < AutoScrollZone ? -(AutoScrollZone - y) : y > height - AutoScrollZone ? y - (height - AutoScrollZone) : 0;
        if (step != 0)
        {
            ThumbnailScroller.Offset = new Vector(0, Math.Max(0, ThumbnailScroller.Offset.Y + step / 2));
        }
    }

    /// <summary>The check box toggles the mark without changing the current page.</summary>
    private void OnThumbnailCheckClick(object? sender, RoutedEventArgs e)
    {
        if (Overview is { } overview && PageAt(sender) is { } page)
        {
            overview.Toggle(page.Index);
        }
    }

    private void OnToolMoveUp(object? sender, RoutedEventArgs e) => Overview?.MoveUpCommand.Execute(null);

    private void OnToolMoveDown(object? sender, RoutedEventArgs e) => Overview?.MoveDownCommand.Execute(null);

    private void OnToolRotateLeft(object? sender, RoutedEventArgs e) => Overview?.RotateLeftCommand.Execute(null);

    private void OnToolRotateRight(object? sender, RoutedEventArgs e) => Overview?.RotateRightCommand.Execute(null);

    private void OnToolDelete(object? sender, RoutedEventArgs e)
    {
        if (Overview is { CanDelete: true } overview)
        {
            overview.DeleteCommand.Execute(null);
        }
    }

    private static PageViewModel? PageAt(object? source) =>
        (source as StyledElement)?.DataContext as PageViewModel;

    private static bool IsInside<T>(object? source) where T : class
    {
        for (var element = source as StyledElement; element is not null; element = element.Parent)
        {
            if (element is T)
            {
                return true;
            }
        }
        return false;
    }

    // ---- Page overview width -----------------------------------------------------------------

    private void OnSplitterPressed(object? sender, PointerPressedEventArgs e)
    {
        if (SidebarSizer is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _splitterStartX = e.GetPosition(this).X;
        _splitterStartWidth = SidebarSizer.Width;
        e.Pointer.Capture(SidebarSplitter);
        e.Handled = true;
    }

    private void OnSplitterMoved(object? sender, PointerEventArgs e)
    {
        if (SidebarSizer is null || double.IsNaN(_splitterStartX))
        {
            return;
        }
        // The main view keeps a minimum width, so the overview never covers the whole window.
        var limit = Bounds.Width - (Application.Current?.Resources.TryGetResource("Layout.MainAreaMinWidth", null, out var min) == true && min is double m ? m : 320);
        var delta = e.GetPosition(this).X - _splitterStartX;
        // In right-to-left layouts the overview is on the right, so the direction turns round.
        SidebarSizer.Apply(_splitterStartWidth + (FlowDirection == Avalonia.Media.FlowDirection.RightToLeft ? -delta : delta), limit);
    }

    private void OnSplitterReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (double.IsNaN(_splitterStartX))
        {
            return;
        }
        _splitterStartX = double.NaN;
        e.Pointer.Capture(null);
        SidebarSizer?.Save();
    }

    // ---- Main view --------------------------------------------------------------------------

    private void OnPageShown(object? sender, int index) => ViewModel?.NotifyPageShown(index);

    /// <summary>Scrolls so that the page starts at the top of the visible area.</summary>
    private void ScrollToPage(int index)
    {
        PageList.ScrollIntoView(index);
        Dispatcher.UIThread.Post(() =>
        {
            if (PageList.ContainerFromIndex(index) is { } container && container.TranslatePoint(default, PageScroller) is { } top)
            {
                var y = Math.Max(0, PageScroller.Offset.Y + top.Y - PageList.Margin.Top);
                PageScroller.Offset = new Vector(PageScroller.Offset.X, y);
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>The current page is the one at the upper quarter of the visible area.</summary>
    private void UpdateCurrentPage()
    {
        if (ViewModel is not { HasPages: true } viewModel)
        {
            return;
        }
        var line = PageScroller.Viewport.Height / 4;
        foreach (var container in PageList.GetRealizedContainers())
        {
            if (container.TranslatePoint(default, PageScroller) is not { } top)
            {
                continue;
            }
            if (top.Y <= line && top.Y + container.Bounds.Height >= line)
            {
                viewModel.Overview.CurrentPage = PageList.IndexFromContainer(container) + 1;
                return;
            }
        }
    }
}
