using AgentFrameworkSolution.Domain.ValueObjects;
using Xunit;

namespace AgentFrameworkSolution.Domain.Tests.ValueObjects;

public sealed class ImageAnalysisResultTests
{
    [Fact]
    public void Empty_ReturnsDefaultInstance()
    {
        var result = ImageAnalysisResult.Empty;

        Assert.Equal(string.Empty, result.Summary);
        Assert.Empty(result.Insights);
        Assert.Empty(result.Tags);
        Assert.Equal(SupportedLanguage.English, result.Language);
    }

    [Fact]
    public void Constructor_WhenLanguageOmitted_DefaultsToEnglish()
    {
        var result = new ImageAnalysisResult("summary", ["i1"], ["t1"]);

        Assert.Equal(SupportedLanguage.English, result.Language);
    }

    [Fact]
    public void Constructor_PreservesProvidedValues()
    {
        IReadOnlyList<string> insights = ["insight-1", "insight-2"];
        IReadOnlyList<string> tags = ["tag-1", "tag-2"];

        var result = new ImageAnalysisResult("summary", insights, tags, SupportedLanguage.German);

        Assert.Equal("summary", result.Summary);
        Assert.Same(insights, result.Insights);
        Assert.Same(tags, result.Tags);
        Assert.Equal(SupportedLanguage.German, result.Language);
    }

    [Fact]
    public void RecordEquality_WhenReferencesMatch_TreatsInstancesAsEqual()
    {
        IReadOnlyList<string> insights = ["i1"];
        IReadOnlyList<string> tags = ["t1"];
        var left = new ImageAnalysisResult("summary", insights, tags, SupportedLanguage.French);
        var right = new ImageAnalysisResult("summary", insights, tags, SupportedLanguage.French);

        Assert.Equal(left, right);
    }
}