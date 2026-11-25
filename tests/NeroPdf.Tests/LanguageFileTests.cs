using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using NeroPdf.Core.Localization;

namespace NeroPdf.Tests;

/// <summary>Checks that every language file has exactly the texts of the English file.</summary>
public partial class LanguageFileTests
{
    private static IReadOnlyDictionary<string, string> Files => LanguageFiles.Find(RepoPaths.Languages);

    public static TheoryData<string> Languages => new(LanguageFiles.Find(RepoPaths.Languages).Keys.Order());

    [Fact]
    public void LaunchLanguagesArePresent()
    {
        string[] expected = ["cs", "da", "de", "el", "en", "en-GB", "es", "fi", "fr", "hu", "it", "ja", "ko", "nb", "nl", "pl", "pt", "pt-BR", "ru", "sv", "th", "tr", "zh", "zh-Hant"];
        Assert.Equal(expected.Order(StringComparer.OrdinalIgnoreCase), Files.Keys.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void LanguageFileHasExactlyTheEnglishKeys(string language)
    {
        var english = LanguageFiles.Read(Files["en"]);
        var other = LanguageFiles.Read(Files[language]);

        Assert.Empty(english.Keys.Except(other.Keys));
        Assert.Empty(other.Keys.Except(english.Keys));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void TextsAreNotEmptyAndKeepPlaceholders(string language)
    {
        var english = LanguageFiles.Read(Files["en"]);
        var other = LanguageFiles.Read(Files[language]);

        foreach (var (key, text) in other)
        {
            Assert.False(string.IsNullOrWhiteSpace(text), $"{language}: {key} is empty");
            Assert.Equal(Placeholders(english[key]), Placeholders(text));
        }
    }

    [Theory]
    [InlineData("de-DE", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en-GB")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-PT", "pt")]
    [InlineData("nb-NO", "nb")]
    [InlineData("zh-CN", "zh")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("fr-CA", "fr")]
    [InlineData("is-IS", "en")]
    public void CultureSelectionFollowsTheParentChain(string culture, string expected)
    {
        Assert.Equal(expected, Localizer.SelectCulture(Files.Keys, CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void MissingTextFallsBackToEnglishThenToId()
    {
        var localizer = Localizer.Load(RepoPaths.Languages, CultureInfo.GetCultureInfo("de-DE"), NullLogger.Instance);

        Assert.Equal("de", localizer.CultureName);
        Assert.Equal("Unknown.Id", localizer.Get("Unknown.Id"));
    }

    private static string[] Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Value).Distinct().Order().ToArray();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();
}
