using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using NeroPdf.Core.Pdf;
using NeroPdf.App.ViewModels;

namespace NeroPdf.App.Dialogs;

/// <summary>Asks how a converted image is placed on the page. Closes with an <see cref="ImagePageChoice"/> or null.</summary>
public partial class ImagePageDialog : Window
{
    public ImagePageDialog()
    {
        InitializeComponent();
    }

    private Size _imageSize = new(3, 4);

    public ImagePageDialog(string filePath, ImagePageChoice last) : this()
    {
        FileName.Text = Path.GetFileName(filePath);
        LoadPreviewImage(filePath);
        OriginalOption.IsChecked = last.Format == ImagePageFormat.Original;
        PortraitOption.IsChecked = last.Format == ImagePageFormat.A4Portrait;
        LandscapeOption.IsChecked = last.Format == ImagePageFormat.A4Landscape;
        Zoom.Value = last.ZoomPercent;
        ShowZoom();

        Zoom.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                ShowZoom();
            }
        };
        OriginalOption.IsCheckedChanged += (_, _) => ShowZoom();
        PortraitOption.IsCheckedChanged += (_, _) => ShowZoom();
        LandscapeOption.IsCheckedChanged += (_, _) => ShowZoom();
        Closed += (_, _) => (PreviewImage.Source as IDisposable)?.Dispose();
        Start.Click += (_, _) => Close(CreateChoice());
        Cancel.Click += (_, _) => Close(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
            }
        };
    }

    /// <summary>The zoom only matters on A4.</summary>
    private void ShowZoom()
    {
        ZoomGroup.IsEnabled = OriginalOption.IsChecked != true;
        ZoomText.Text = ((int)Zoom.Value).ToString(CultureInfo.CurrentCulture) + " %";
        UpdatePreview();
    }

    /// <summary>Images the preview cannot read (e.g. some TIFF files) show an empty page.</summary>
    private void LoadPreviewImage(string filePath)
    {
        try
        {
            var bitmap = new Bitmap(filePath);
            PreviewImage.Source = bitmap;
            _imageSize = bitmap.Size;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            PreviewImage.Source = null;
        }
    }

    /// <summary>Same placement as in the PDF (see PdfPageLayout), scaled into the preview area.</summary>
    private void UpdatePreview()
    {
        if (PreviewPage is null || _imageSize.Width <= 0 || _imageSize.Height <= 0)
        {
            return;
        }
        var box = Application.Current?.TryGetResource("Layout.ImagePreviewSize", null, out var value) == true && value is double size ? size : 200;
        var layout = CreateChoice().ToLayout();
        var (pageWidth, pageHeight) = layout is null ? (_imageSize.Width, _imageSize.Height) : (layout.Width, layout.Height);
        var pageScale = Math.Min(box / pageWidth, box / pageHeight);
        PreviewPage.Width = pageWidth * pageScale;
        PreviewPage.Height = pageHeight * pageScale;

        var imageScale = layout is null
            ? pageScale * pageWidth / _imageSize.Width
            : Math.Min(layout.Width / _imageSize.Width, layout.Height / _imageSize.Height) * layout.Zoom * pageScale;
        PreviewImage.Width = _imageSize.Width * imageScale;
        PreviewImage.Height = _imageSize.Height * imageScale;
    }

    private ImagePageChoice CreateChoice() => new(
        PortraitOption.IsChecked == true ? ImagePageFormat.A4Portrait
            : LandscapeOption.IsChecked == true ? ImagePageFormat.A4Landscape
            : ImagePageFormat.Original,
        (int)Zoom.Value);
}
