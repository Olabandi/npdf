using System.Text.Json.Serialization;

namespace NeroPdf.App.Infrastructure;

/// <summary>
/// Source-generated JSON reading for the app's data files (themes, layout, links). Needed because the
/// Store package is trimmed, which removes reflection-based serialization.
/// </summary>
[JsonSerializable(typeof(ThemeManager.ThemeFile))]
[JsonSerializable(typeof(Dictionary<string, double>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
