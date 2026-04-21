using AgentFrameworkSolution.Domain.ValueObjects;
using Xunit;

namespace AgentFrameworkSolution.Domain.Tests.ValueObjects;

public sealed class SupportedLanguageExtensionsTests
{
    [Theory]
    [InlineData(SupportedLanguage.English, "English")]
    [InlineData(SupportedLanguage.Spanish, "Spanish")]
    [InlineData(SupportedLanguage.Italian, "Italian")]
    [InlineData(SupportedLanguage.French, "French")]
    [InlineData(SupportedLanguage.German, "German")]
    public void GetLanguageName_WhenLanguageSupported_ReturnsExpectedName(
        SupportedLanguage language,
        string expectedName)
    {
        var result = language.GetLanguageName();

        Assert.Equal(expectedName, result);
    }

    [Fact]
    public void GetLanguageName_WhenLanguageInvalid_ThrowsArgumentOutOfRangeException()
    {
        var language = (SupportedLanguage)999;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => language.GetLanguageName());

        Assert.Equal("language", exception.ParamName);
    }

    [Theory]
    [InlineData("English", SupportedLanguage.English)]
    [InlineData("SPANISH", SupportedLanguage.Spanish)]
    [InlineData("italian", SupportedLanguage.Italian)]
    [InlineData("FrEnCh", SupportedLanguage.French)]
    [InlineData("german", SupportedLanguage.German)]
    public void TryParse_WhenValueValid_IgnoresCaseAndReturnsParsedLanguage(
        string value,
        SupportedLanguage expectedLanguage)
    {
        var success = SupportedLanguageExtensions.TryParse(value, out var language);

        Assert.True(success);
        Assert.Equal(expectedLanguage, language);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_WhenValueMissing_DefaultsToEnglish(string? value)
    {
        var success = SupportedLanguageExtensions.TryParse(value, out var language);

        Assert.True(success);
        Assert.Equal(SupportedLanguage.English, language);
    }

    [Fact]
    public void TryParse_WhenValueInvalid_ReturnsFalseAndLeavesLanguageAtEnglish()
    {
        var success = SupportedLanguageExtensions.TryParse("Klingon", out var language);

        Assert.False(success);
        Assert.Equal(SupportedLanguage.English, language);
    }
}