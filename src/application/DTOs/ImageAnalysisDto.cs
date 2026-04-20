namespace AgentFrameworkSolution.Application.DTOs;

public record ImageAnalysisDto(
    Guid Id,
    string FileName,
    string Summary,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Tags,
    DateTime AnalyzedAt);
