using Avalonia;

namespace NeroPdf.App.Infrastructure;

/// <summary>
/// Width of the page overview. The user drags its edge; the thumbnails grow and shrink with it.
/// The smallest width is the one from Config/layout.json.
/// </summary>
public sealed class SidebarSizer
{
    private readonly Application _application;
    private readonly double _minWidth;
    private readonly double _maxWidth;
    private readonly double _baseThumbnailWidth;
    private readonly double _baseGapHeight;
    private readonly Action<double> _save;

    public SidebarSizer(Application application, ThemeManager themes, double savedWidth, Action<double> save)
    {
        _application = application;
        _minWidth = themes.LayoutValue("SidebarWidth", 244);
        _maxWidth = themes.LayoutValue("SidebarMaxWidth", 640);
        _baseThumbnailWidth = themes.LayoutValue("ThumbnailWidth", 92);
        _baseGapHeight = themes.LayoutValue("DropGapHeight", 140);
        _save = save;
        Apply(savedWidth > 0 ? savedWidth : _minWidth);
    }

    public double Width { get; private set; }

    /// <summary>Sets the width, kept between the minimum and <paramref name="limit"/> (e.g. part of the window).</summary>
    public void Apply(double width, double limit = double.MaxValue)
    {
        var max = Math.Max(_minWidth, Math.Min(_maxWidth, limit));
        Width = Math.Clamp(width, _minWidth, max);
        var thumbnail = _baseThumbnailWidth + (Width - _minWidth);
        var resources = _application.Resources;
        resources["Layout.SidebarWidth"] = Width;
        resources["Layout.ThumbnailWidth"] = thumbnail;
        resources["Layout.DropGapHeight"] = _baseGapHeight * thumbnail / _baseThumbnailWidth;
    }

    /// <summary>Remembers the current width for the next start.</summary>
    public void Save() => _save(Width);
}
