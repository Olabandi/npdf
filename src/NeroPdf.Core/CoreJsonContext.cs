using System.Text.Json.Serialization;
using NeroPdf.Core.Settings;

namespace NeroPdf.Core;

/// <summary>
/// Source-generated JSON serialization for Core. Needed because the printer task is compiled ahead of
/// time (Native AOT), where reflection-based serialization is not available.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
