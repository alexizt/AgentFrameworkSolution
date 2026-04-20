namespace AgentFrameworkSolution.Domain.ValueObjects;

public record ImageAnalysisResult(
    string Summary,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Tags,
    SupportedLanguage Language = SupportedLanguage.English)
{
    public static ImageAnalysisResult Empty =>
        new(string.Empty, [], [], SupportedLanguage.English);
}
