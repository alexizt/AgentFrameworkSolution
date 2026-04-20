namespace AgentFrameworkSolution.Domain.ValueObjects;

public record ImageAnalysisResult(
    string Summary,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Tags)
{
    public static ImageAnalysisResult Empty =>
        new(string.Empty, [], []);
}
