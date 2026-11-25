using Avalonia.Markup.Xaml;
using NeroPdf.Core.Localization;

namespace NeroPdf.App.Infrastructure;

/// <summary>Holds the active localizer for the user interface.</summary>
public static class Texts
{
    public static Localizer? Current { get; internal set; }

    public static string Get(string id) => Current?.Get(id) ?? id;

    public static string Format(string id, params object?[] args) => Current?.Format(id, args) ?? id;
}

/// <summary>XAML access to translated texts: <c>Text="{l:Text Viewer.Empty}"</c>.</summary>
public sealed class TextExtension : MarkupExtension
{
    public TextExtension()
    {
    }

    public TextExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Texts.Get(Key);
}
